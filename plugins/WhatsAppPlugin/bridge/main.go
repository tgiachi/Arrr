package main

import (
	"context"
	"encoding/json"
	"fmt"
	"os"
	"os/signal"
	"syscall"
	"time"

	_ "github.com/mattn/go-sqlite3"
	"go.mau.fi/whatsmeow"
	"go.mau.fi/whatsmeow/store/sqlstore"
	"go.mau.fi/whatsmeow/types/events"
	waLog "go.mau.fi/whatsmeow/util/log"
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
	if len(os.Args) > 1 {
		sessionPath = os.Args[1]
	}

	log := stderrLog{prefix: "[WA]"}

	ctx := context.Background()

	container, err := sqlstore.New(ctx, "sqlite3", "file:"+sessionPath+"?_foreign_keys=on", log)
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

	sig := make(chan os.Signal, 1)
	signal.Notify(sig, os.Interrupt, syscall.SIGTERM)
	<-sig
	client.Disconnect()
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
