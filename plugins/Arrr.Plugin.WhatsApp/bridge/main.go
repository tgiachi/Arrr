package main

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"os/signal"
	"strconv"
	"syscall"
	"time"

	_ "modernc.org/sqlite"
	"go.mau.fi/whatsmeow"
	"go.mau.fi/whatsmeow/proto/waE2E"
	"go.mau.fi/whatsmeow/store/sqlstore"
	"go.mau.fi/whatsmeow/types"
	"go.mau.fi/whatsmeow/types/events"
	waLog "go.mau.fi/whatsmeow/util/log"
	"google.golang.org/protobuf/proto"
)

// Event is the NDJSON envelope written to stdout.
type Event struct {
	Type    string `json:"type"`
	Code    string `json:"code,omitempty"`
	JID     string `json:"jid,omitempty"`
	Name    string `json:"name,omitempty"`
	From    string `json:"from,omitempty"`
	FromJID string `json:"fromJid,omitempty"`
	Chat    string `json:"chat,omitempty"`
	ChatJID string `json:"chatJid,omitempty"`
	Body    string `json:"body,omitempty"`
	TS      string `json:"ts,omitempty"`
}

var enc = json.NewEncoder(os.Stdout)

func emit(e Event) { _ = enc.Encode(e) }

// stderrLog implements waLog.Logger; routes only errors to stderr so stdout stays clean NDJSON.
type stderrLog struct{ prefix string }

func (l stderrLog) Debugf(_ string, _ ...interface{}) {}
func (l stderrLog) Infof(_ string, _ ...interface{})  {}
func (l stderrLog) Warnf(_ string, _ ...interface{})  {}
func (l stderrLog) Errorf(msg string, args ...interface{}) {
	fmt.Fprintf(os.Stderr, l.prefix+" "+msg+"\n", args...)
}
func (l stderrLog) Sub(module string) waLog.Logger { return stderrLog{prefix: "[" + module + "]"} }

func main() {
	sessionPath := "whatsapp.db"
	httpPort := 8765

	for i := 1; i < len(os.Args); i++ {
		switch os.Args[i] {
		case "--session":
			if i+1 < len(os.Args) {
				i++
				sessionPath = os.Args[i]
			}
		case "--http-port":
			if i+1 < len(os.Args) {
				i++
				if p, err := strconv.Atoi(os.Args[i]); err == nil {
					httpPort = p
				}
			}
		default:
			sessionPath = os.Args[i]
		}
	}

	log := stderrLog{prefix: "[WA]"}

	ctx := context.Background()

	container, err := sqlstore.New(ctx, "sqlite", "file:"+sessionPath+"?_pragma=foreign_keys(1)&_pragma=busy_timeout(5000)", log)
	if err != nil {
		fmt.Fprintf(os.Stderr, "store: %v\n", err)
		os.Exit(1)
	}

	deviceStore, err := container.GetFirstDevice(ctx)
	if err != nil {
		fmt.Fprintf(os.Stderr, "device: %v\n", err)
		os.Exit(1)
	}

	client := whatsmeow.NewClient(deviceStore, log)
	client.AddEventHandler(handleMsg)

	if client.Store.ID == nil {
		qrChan, _ := client.GetQRChannel(ctx)
		if err = client.Connect(); err != nil {
			fmt.Fprintf(os.Stderr, "connect: %v\n", err)
			os.Exit(1)
		}
		for evt := range qrChan {
			if evt.Event == "code" {
				emit(Event{Type: "qr", Code: evt.Code})
			} else {
				fmt.Fprintf(os.Stderr, "login event: %s\n", evt.Event)
			}
		}
	} else {
		if err = client.Connect(); err != nil {
			fmt.Fprintf(os.Stderr, "connect: %v\n", err)
			os.Exit(1)
		}
	}

	jid := ""
	if client.Store.ID != nil {
		jid = client.Store.ID.String()
	}
	emit(Event{Type: "ready", JID: jid, Name: client.Store.PushName})

	go startHTTP(client, httpPort)

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, os.Interrupt, syscall.SIGTERM)
	<-sig
	client.Disconnect()
}

type sendRequest struct {
	To   string `json:"to"`
	Body string `json:"body"`
}

func startHTTP(client *whatsmeow.Client, port int) {
	mux := http.NewServeMux()

	mux.HandleFunc("/send", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		var req sendRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			http.Error(w, "bad request: "+err.Error(), http.StatusBadRequest)
			return
		}
		if req.To == "" || req.Body == "" {
			http.Error(w, "to and body are required", http.StatusBadRequest)
			return
		}
		jid, err := types.ParseJID(req.To)
		if err != nil {
			http.Error(w, "invalid JID: "+err.Error(), http.StatusBadRequest)
			return
		}
		_, err = client.SendMessage(r.Context(), jid, &waE2E.Message{
			Conversation: proto.String(req.Body),
		})
		if err != nil {
			http.Error(w, "send failed: "+err.Error(), http.StatusInternalServerError)
			return
		}
		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(map[string]bool{"ok": true})
	})

	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(map[string]bool{"connected": client.IsConnected()})
	})

	addr := fmt.Sprintf("127.0.0.1:%d", port)
	fmt.Fprintf(os.Stderr, "[WA] HTTP server listening on %s\n", addr)
	srv := &http.Server{Addr: addr, Handler: mux, ReadTimeout: 10 * time.Second, WriteTimeout: 10 * time.Second}
	if err := srv.ListenAndServe(); err != nil {
		fmt.Fprintf(os.Stderr, "[WA] HTTP server error: %v\n", err)
	}
}

func handleMsg(raw interface{}) {
	evt, ok := raw.(*events.Message)
	if !ok || evt.Info.IsFromMe {
		return
	}

	body := evt.Message.GetConversation()
	if body == "" {
		if ext := evt.Message.GetExtendedTextMessage(); ext != nil {
			body = ext.GetText()
		}
	}
	if body == "" {
		return
	}

	chat := ""
	if evt.Info.IsGroup {
		chat = evt.Info.Chat.User
	}

	emit(Event{
		Type:    "message",
		From:    evt.Info.PushName,
		FromJID: evt.Info.Sender.String(),
		Chat:    chat,
		ChatJID: evt.Info.Chat.String(),
		Body:    body,
		TS:      evt.Info.Timestamp.UTC().Format(time.RFC3339),
	})
}
