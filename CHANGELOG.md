## [1.8.0](https://github.com/tgiachi/Arrr/compare/v1.7.0...v1.8.0) (2026-04-28)

### Features

* **debug:** gate debug tab on service IsDebug flag, not npm DEV mode ([c488ab7](https://github.com/tgiachi/Arrr/commit/c488ab7fcd7954fdf4f8ae63278d98d20b2db39e)), closes [#if](https://github.com/tgiachi/Arrr/issues/if)
* **docker:** add Docker image with CI/CD push to Docker Hub ([9b75f0c](https://github.com/tgiachi/Arrr/commit/9b75f0c4d485b62616b185c81ba3e167758ca15f))
* **gitlab:** add CI pipeline status monitoring ([460338a](https://github.com/tgiachi/Arrr/commit/460338ab45c2d6b0da762efe6ec947dd6fc35658))
* **gitlab:** support multiple GitLab server instances ([b76a5f0](https://github.com/tgiachi/Arrr/commit/b76a5f02a77b0a12c657915cabfc1af7474d6d06))
* **grpc:** add gRPC streaming endpoint for remote notification clients ([8827592](https://github.com/tgiachi/Arrr/commit/8827592d29ed3c7474094dea4875770d63b7bd17))
* **plugin:** add GitLab source plugin ([f4ec980](https://github.com/tgiachi/Arrr/commit/f4ec980089ade7fc954c9a22beb6d3746d0d9424))
* **plugins:** add ITestablePlugin interface for config validation ([f199fb9](https://github.com/tgiachi/Arrr/commit/f199fb96cf0af10c71efeaf8f57e9bff5677c36d))
* **plugins:** add ITestableSink interface and TestAsync to all plugins ([3d854c5](https://github.com/tgiachi/Arrr/commit/3d854c5d7a2932ca0fbe2d3576f66f1cd0e7d8c2))
* **tray,service:** replace gRPC with SignalR, tray improvements ([ec65e6c](https://github.com/tgiachi/Arrr/commit/ec65e6c86ed8b8b040b6223ba0d5be29844694d2))
* **tray:** add About window, icon support and D-Bus improvements ([5da92af](https://github.com/tgiachi/Arrr/commit/5da92afc8c4e1fe5930d7a7ebee00b9f5d9b2e58))
* **tray:** add AppSettings model and SettingsService ([81ba09a](https://github.com/tgiachi/Arrr/commit/81ba09a0ba1fd291d11d2396f6cbd5538e91e27b))
* **tray:** add connection status item with green/red icon (20px) ([ae681e6](https://github.com/tgiachi/Arrr/commit/ae681e62af8c43f36c95cba09732beb34c32e544))
* **tray:** add gRPC client wrapper with DND and subscription retry ([44ddcf8](https://github.com/tgiachi/Arrr/commit/44ddcf828a1d6631173138fce8376087854fc787))
* **tray:** add TrayViewModel with DND toggle, settings and exit commands ([335fca9](https://github.com/tgiachi/Arrr/commit/335fca92dcea7d32656c88ffd459dc40916660a1))
* **tray:** app shell with tray icon wiring ([3e266b6](https://github.com/tgiachi/Arrr/commit/3e266b6ac8dfe523238e4da8bd2b291c1f87e568))
* **tray:** forward NotificationEvent to desktop via notify-send ([f7b6f7b](https://github.com/tgiachi/Arrr/commit/f7b6f7bf3d672e77cc1941f125492a7f4cb001cf))
* **tray:** packaging for deb/rpm/AUR (arrr-tray-bin) ([c6098f9](https://github.com/tgiachi/Arrr/commit/c6098f9bbdf8037f7769ae1ecb00373b4fd57614))
* **tray:** restore Tmds.DBus for D-Bus notification delivery ([ca8a1c7](https://github.com/tgiachi/Arrr/commit/ca8a1c7374d47e49241346b050b6aaaf94801c71))
* **tray:** scaffold Avalonia 12 MVVM project with gRPC proto and assets ([b63d9ae](https://github.com/tgiachi/Arrr/commit/b63d9ae4b598fa87916042ee6b60efe80af761e2))
* **tray:** settings window + view model ([712e2e1](https://github.com/tgiachi/Arrr/commit/712e2e173568ac6dafc7369c8bb5893fe7f49952))
* **tray:** show server version in context menu when connected ([ba86b1e](https://github.com/tgiachi/Arrr/commit/ba86b1e3295028718a804b4a2b05b556acda198e))
* **ui:** add Debug tab with notification injector (DEV only) ([51302c3](https://github.com/tgiachi/Arrr/commit/51302c3b0329650f6b285133fdc502a94ecce458))

### Bug Fixes

* **ci:** strip v prefix from VERSION before dotnet build/test ([0709fe1](https://github.com/tgiachi/Arrr/commit/0709fe1393340d6e04194d4cbeff9e0669981440))
* **docker:** copy full publish dir to include libe_sqlcipher.so and wwwroot; fix runtime-deps tag ([0dd9c2a](https://github.com/tgiachi/Arrr/commit/0dd9c2a16bdc7eecd5c0b4d27692dfe62c8615b8))
* **tray,service:** separate gRPC port (Http2) from REST port (Http1) ([19adb44](https://github.com/tgiachi/Arrr/commit/19adb44bdc8c63717c0bd9da4a69ec037b035de5))
* **tray:** connection status stuck on Disconnected ([736c4c9](https://github.com/tgiachi/Arrr/commit/736c4c984d32103be103b4c20ccc97ef990fbeaf))
* **tray:** defer settings window show past menu-close event ([6560e59](https://github.com/tgiachi/Arrr/commit/6560e59f3be2f5c367dd4e42c1dcb7756a4ebac6))
* **tray:** fire SubscriptionConnected only after first successful MoveNext ([e02d141](https://github.com/tgiachi/Arrr/commit/e02d141dd5a125dc485c99233624705f8c74f413))
* **tray:** guard OpenSettings dispatcher action + make Exit bulletproof ([443d204](https://github.com/tgiachi/Arrr/commit/443d204ce56c195b72edbb26abb6ecf141a59811))
* **tray:** make IFreedesktopNotifications public for Tmds.DBus proxy codegen ([7235017](https://github.com/tgiachi/Arrr/commit/7235017a50d582a0babe3f4c78f186187db86805))
* **tray:** remove IsEnabled=false from version and status menu items ([c9e4777](https://github.com/tgiachi/Arrr/commit/c9e4777246c9efc6f34a668d85215175442de2ff))
* **tray:** send x-api-key header on all gRPC calls ([a97df89](https://github.com/tgiachi/Arrr/commit/a97df89038e43a18d4d55e2bbca7c8261d3633c5))
* **tray:** use correct assembly name in avares:// URI for icon loading ([b543c0c](https://github.com/tgiachi/Arrr/commit/b543c0c9e7ee56d2094a57d8851b22f8e03c7d02))
* **ui:** redesign DebugView with semantic tokens for light/dark theme ([53240a5](https://github.com/tgiachi/Arrr/commit/53240a5a8175d2db33a8ad8f75c7e63f0d3ddd03))
* **ui:** remove scanline overlay from DebugView ([886284c](https://github.com/tgiachi/Arrr/commit/886284c6d8d3f90f397ea1c46a0deccb5eb7b463))
* **whatsapp:** swap title and body in notifications ([2afd79e](https://github.com/tgiachi/Arrr/commit/2afd79ec0669fe042fcf4e93d77fcd82ba19499f))

## [1.7.0](https://github.com/tgiachi/Arrr/compare/v1.6.0...v1.7.0) (2026-04-27)

### Features

* **aur:** add arrr-git AUR package (build from source) ([e48ce8c](https://github.com/tgiachi/Arrr/commit/e48ce8c470128a1f3459212360e48ecde5a44ac1))
* **core:** expose shared HttpClient on IPluginContext ([0b623e0](https://github.com/tgiachi/Arrr/commit/0b623e046e44050dbdf6b7777d6878645fafa582))
* **digest:** expose digest config in daemon API and add schedule editor UI ([8d26516](https://github.com/tgiachi/Arrr/commit/8d265163e0715160ae2b9e53696fe0771848417f))
* **digest:** move digest scheduling to service, extend CalDav with IDigestProvider ([da54079](https://github.com/tgiachi/Arrr/commit/da540793d5fc10a2c2639de8430635e819760ee9))
* **dnd:** add Do Not Disturb toggle — backend endpoint + UI ([9e819ae](https://github.com/tgiachi/Arrr/commit/9e819ae9c2b96bee8fa4f739732447b05ccaa373))
* **routing-ui:** add up/down reorder buttons for rules ([18edad1](https://github.com/tgiachi/Arrr/commit/18edad103d4c21f58129ad6b11399980716a88ae))
* **routing-ui:** replace up/down buttons with drag-and-drop reorder ([5415714](https://github.com/tgiachi/Arrr/commit/5415714785b06464524c3ce2841d918afbab09c3))
* **routing:** add notification routing rules engine + UI ([2bd90a3](https://github.com/tgiachi/Arrr/commit/2bd90a3f76188b5af8d78ebfe919a398dc71751d))
* **todoist:** due date alerts + reminders logic (TDD) ([6a22912](https://github.com/tgiachi/Arrr/commit/6a2291223a0ccf0a9064813468e31b3b87337f16))
* **todoist:** implement IDigestProvider for morning/evening digest integration ([987dff9](https://github.com/tgiachi/Arrr/commit/987dff95f1c6ecfd19ebdcd88333f021d9cc2063))
* **todoist:** plugin shell + empty-token guard (TDD) ([c6c340a](https://github.com/tgiachi/Arrr/commit/c6c340aaf7efeefdeba536ab435492280ea3537d))
* **todoist:** scaffold project, DTOs, and config ([d5c9069](https://github.com/tgiachi/Arrr/commit/d5c90691562fbaabe3a8a2d261862cdd7cc0d28c))

### Bug Fixes

* add EnableWindowsTargeting to WindowsNotify so CI can restore on Linux ([43dd0a8](https://github.com/tgiachi/Arrr/commit/43dd0a84ee0990529ac0458a4d5c093b1f4fd3bb))
* build only Linux-compatible projects in CI ([2ebcaf4](https://github.com/tgiachi/Arrr/commit/2ebcaf4577302322ca48f366fed399ee797f7c2f))
* build only Linux-compatible projects in CI (exclude WindowsNotify) ([2b8acf3](https://github.com/tgiachi/Arrr/commit/2b8acf3b0083a798d7defae561e7a7c99b19e52e))
* CI hotfixes — WindowsNotify usings, WhatsApp bridge path, AUR SSH key encoding ([eb730b4](https://github.com/tgiachi/Arrr/commit/eb730b486a7d7913b8b908b70ba751578ab61278))
* **ci:** use Get-ChildItem to push nupkg on Windows runner ([83113db](https://github.com/tgiachi/Arrr/commit/83113dbb5a7910f0409db47971cdedcb8aa0ef8b))
* EnableWindowsTargeting for Linux CI restore ([147b546](https://github.com/tgiachi/Arrr/commit/147b54660abe06a9f96e53a39f6057828f9b51a4))
* restore usings in WindowsNotify, fix WhatsApp bridge path, base64-encode AUR key ([6d75a18](https://github.com/tgiachi/Arrr/commit/6d75a18cc9263481dca45fd6b7d9eadcdca7f4a3))
* **routing-ui:** use placeholder for allow-sinks instead of * as text value ([c98023a](https://github.com/tgiachi/Arrr/commit/c98023af855bb451458f55f7d1c953ab45980fc6))
* **routing:** only record block/restrict events in history ring buffer ([8aebd47](https://github.com/tgiachi/Arrr/commit/8aebd473795d77950e9ad11dac4f6988fef04eae))
* **ui:** ux review — fix reconciliation bug, emoji icons, accessibility and consistency ([1051305](https://github.com/tgiachi/Arrr/commit/10513055b1dfbfdc73327cac840773e04ef6f88b))
* **ui:** visual polish — ambient gradient, card elevation, contrast, motion ([ecc0ac6](https://github.com/tgiachi/Arrr/commit/ecc0ac685f5c93dde3d591d3b9e9b9cc61fd6ea8)), closes [#374151](https://github.com/tgiachi/Arrr/issues/374151) [#52627a](https://github.com/tgiachi/Arrr/issues/52627a) [#374151](https://github.com/tgiachi/Arrr/issues/374151) [#3d4f66](https://github.com/tgiachi/Arrr/issues/3d4f66)
* **windows-notify:** add missing Microsoft.Toolkit.Uwp.Notifications using ([451f1ea](https://github.com/tgiachi/Arrr/commit/451f1ea4900d813db75a91f7af3a6cca8316b59e))

## [1.6.0](https://github.com/tgiachi/Arrr/compare/v1.5.1...v1.6.0) (2026-04-26)

### Features

* add /api/version endpoint and footer in UI ([8c904b4](https://github.com/tgiachi/Arrr/commit/8c904b4c8a1ff16aed7c6b008be94ef976ce2f78))
* add Arrr.Plugin.Healthcheck source plugin ([0e00817](https://github.com/tgiachi/Arrr/commit/0e00817346117dcdee1853e1a43caadc718365c0))
* add Bark sink plugin ([978121b](https://github.com/tgiachi/Arrr/commit/978121baaa5bda8cdc6ac8a0ff8b07786cd42e13))
* add Daemon Config page — edit ArrrConfig from the UI ([c984fff](https://github.com/tgiachi/Arrr/commit/c984fff9fbc3807945c58400c8d18fe7b072fad0))
* add deduplication filter and macOS notification sink ([10fd0d4](https://github.com/tgiachi/Arrr/commit/10fd0d4cab80fb24caedb4abae7a6d8d319f1e7a))
* add GotifySink, HomeAssistantSink, and MqttSource plugins ([eb3e446](https://github.com/tgiachi/Arrr/commit/eb3e4468444b621f8a01012e2efa4ea2ba7ceda6))
* add platform compatibility check for plugins and sinks ([5dc093e](https://github.com/tgiachi/Arrr/commit/5dc093e9ba10abee673f10dd20a80ff381749bf1))
* add Pushover sink, GitHub source, and systemd journal source plugins ([ef477ea](https://github.com/tgiachi/Arrr/commit/ef477eaec6ef46bba664f2a45bb89c55a5bb0aa2))
* add real-time notification stream via SignalR ([85566da](https://github.com/tgiachi/Arrr/commit/85566dab98243771be457820c7266546310a42f7))
* automate AUR package update on release ([d7066f4](https://github.com/tgiachi/Arrr/commit/d7066f4e4fb6efbb8d65145308ec989f1d537283))
* **core:** enrich Notification with Priority, Url, and Extras ([8845634](https://github.com/tgiachi/Arrr/commit/88456340265e0722f56ffbfe7db5441ea05c7a15))
* encrypt history SQLite DB with SQLCipher ([398ce46](https://github.com/tgiachi/Arrr/commit/398ce468f39ef8587b135314574be0aad127bb51))
* implement full plugin/sink lifecycle management and VersionUtils ([1aebbcf](https://github.com/tgiachi/Arrr/commit/1aebbcfd68de188c418bf61776ee6c2a22f64ba1))
* notification history with opt-in toggle ([207e7e1](https://github.com/tgiachi/Arrr/commit/207e7e1815babf09e6ba1e0223b907aebc4b8440))
* **plugins:** add update-to-latest for installed plugins ([f9f3ef7](https://github.com/tgiachi/Arrr/commit/f9f3ef7ac4996254d5a48a9d402da1cb72a943cc))
* **sink:** add Windows toast notification sink ([135edce](https://github.com/tgiachi/Arrr/commit/135edce51377c8a2119bd34c050eae56119c56e6))
* **ui:** add light/dark mode toggle with pirate parchment theme ([9fa91b0](https://github.com/tgiachi/Arrr/commit/9fa91b03a13a6b109b0321e6aafa92ae755d3901)), closes [#080c14](https://github.com/tgiachi/Arrr/issues/080c14)
* **ui:** add platform filter to install panel + nuget platform tags ([ec744a8](https://github.com/tgiachi/Arrr/commit/ec744a83a608d4f605ca1f4aa5334c054ed0e49b))
* **ui:** add top navbar with Configurazione / Install / Logs tabs ([6c67ae9](https://github.com/tgiachi/Arrr/commit/6c67ae9c8ee76ae0f6cf8f8d1a0c5d37e34d4e49))
* **ui:** replace parchment theme with Steel Mist; add semantic tokens for all surfaces ([d66c741](https://github.com/tgiachi/Arrr/commit/d66c7410a8d6b7802e088bfcc37f01d3c7f6f562)), closes [#f0f4f8](https://github.com/tgiachi/Arrr/issues/f0f4f8)
* **ui:** replace skull icon with logo PNG, fix logs light theme colors ([c6bebc2](https://github.com/tgiachi/Arrr/commit/c6bebc221a96c87f2d31f601ffe69392ffb0bc81))

### Bug Fixes

* clear SQLite connection pool before retrying encrypted DB open ([3e8dbc6](https://github.com/tgiachi/Arrr/commit/3e8dbc6328116c82707a5af97c86bf75bf9c677d))
* correct ExecStart path in arrr.service and add AUR packaging ([4e67244](https://github.com/tgiachi/Arrr/commit/4e672446f300a8ddee98400f8250f352a863c063))
* **installer:** use flat container for version existence check ([78e0daa](https://github.com/tgiachi/Arrr/commit/78e0daab84ea73a822ac8ec369032b07b7e89d38))
* make history search inputs transparent to blend with their containers ([091e9d8](https://github.com/tgiachi/Arrr/commit/091e9d815b7ffb1d69d7ace8dee078067d4c86c4))
* **plugin-loader:** resolve Arrr.Core version mismatch on plugin load ([afce3ba](https://github.com/tgiachi/Arrr/commit/afce3baccfb76c0c48a2d097943e3a8024f8e5d2))
* **plugins:** migrate to MQTTnet v5 and Ical.Net v5 breaking changes ([72c73e9](https://github.com/tgiachi/Arrr/commit/72c73e99f1b0b1dba1511a74192d48002f0c4b28))
* stretch search and source inputs to fill their containers in HistoryView ([5d44068](https://github.com/tgiachi/Arrr/commit/5d440685a3b6edd63bcde985498eda90afb9eac6))
* **ui:** rename tab label Configurazione to Configuration ([d5021a2](https://github.com/tgiachi/Arrr/commit/d5021a2d009f22a2222f02e466d23daea470c86e))
* **ui:** use class attribute for next-themes to match Chakra v3 color mode ([64e84b4](https://github.com/tgiachi/Arrr/commit/64e84b470453bcf30d12cee00df4d0a31c5b0f53))

### Performance Improvements

* **eventbus:** dispatch handlers concurrently with Task.WhenAll ([1bb6bba](https://github.com/tgiachi/Arrr/commit/1bb6bbaf72d612a4a34afb93501e86a90ba4a8f2))

## [1.5.1](https://github.com/tgiachi/Arrr/compare/v1.5.0...v1.5.1) (2026-04-25)

### Bug Fixes

* **ci:** create local-packages dir before dotnet restore to avoid NU1301 ([1fc20e0](https://github.com/tgiachi/Arrr/commit/1fc20e02c1b90974564e2d21e1b1f0fa56ddc940))
* **ci:** pack Arrr.Core into local-packages before restore ([1faf921](https://github.com/tgiachi/Arrr/commit/1faf9219da8d98fa213329933fec088782815abb))

## [1.4.1](https://github.com/tgiachi/Arrr/compare/v1.4.0...v1.4.1) (2026-04-24)

### Bug Fixes

* **bridge:** replace go-sqlite3 (CGO) with modernc.org/sqlite (pure Go) ([c571c85](https://github.com/tgiachi/Arrr/commit/c571c855178b5d0871f9039f1df8dc2e8ed7ebae))

## [1.4.0](https://github.com/tgiachi/Arrr/compare/v1.3.0...v1.4.0) (2026-04-24)

### Features

* **ci:** add multi-platform whatsapp-bridge build and TelegramBotSink/WhatsApp NuGet publishing ([eadf5d1](https://github.com/tgiachi/Arrr/commit/eadf5d10bbe8128fd37e06e260bffa374e3a11fa))
* **sink:** add Arrr.Sink.Telegram — delivers notifications via Telegram Bot API ([38044fc](https://github.com/tgiachi/Arrr/commit/38044fcb0e10b5b7aa58ea4aee63d58652e18ce7))

## [1.3.0](https://github.com/tgiachi/Arrr/compare/v1.2.1...v1.3.0) (2026-04-24)

### Features

* add pluggable sink system (output connectors) ([4de27ef](https://github.com/tgiachi/Arrr/commit/4de27ef810b3ff6989209c723816162ddf64b2f4))
* **sink:** add DbusNotifySink replacing DBusNotifySubscriber ([07be0ae](https://github.com/tgiachi/Arrr/commit/07be0ae94cd6190f5cb7b8219e7024cd30f71923))
* **sink:** add ISinkPlugin, ISinkContext, ISinkManager, AvailableSinkResponse, SinkEntry ([8e9bb7e](https://github.com/tgiachi/Arrr/commit/8e9bb7e93359de049c32774346047b9641787a4c))
* **sink:** add SinksEndpoint + FakeSinkManager with full test coverage ([39a29a7](https://github.com/tgiachi/Arrr/commit/39a29a7d69b6cd3c98b576b135f513a2c02b6f3b))
* **sink:** add UnixSocketSink replacing UnixSocketServer, remove old subscribers ([1781c74](https://github.com/tgiachi/Arrr/commit/1781c74f069b3c8ed7fa44db8f2babe102bea6e5))
* **ui:** show available NuGet plugins with install button ([be4e370](https://github.com/tgiachi/Arrr/commit/be4e3707d1007ea5928e038aed4e35c97a7a3958))
* **ui:** split NuGet panel into plugins/sinks sections with search ([254d5e9](https://github.com/tgiachi/Arrr/commit/254d5e97466a87c13fe23d0663be886ec09a9bbb))

### Bug Fixes

* add arrr-plugin tag to WhatsApp plugin, show icon in NuGet card ([1d8ccf5](https://github.com/tgiachi/Arrr/commit/1d8ccf53b36dc54db8cf055b60430b9fe1c64952))
* **ui:** search NuGet by tag arrr-plugin instead of name prefix ([17d9fdf](https://github.com/tgiachi/Arrr/commit/17d9fdf47ffd4a053acab1be8ee4449436092943))

## [1.2.1](https://github.com/tgiachi/Arrr/compare/v1.2.0...v1.2.1) (2026-04-24)

### Bug Fixes

* **tests:** implement GetPendingQrCode in FakePluginManager ([d58e017](https://github.com/tgiachi/Arrr/commit/d58e017db60d47913754fdf1cc402fa00521b6a7))

## [1.2.0](https://github.com/tgiachi/Arrr/compare/v1.1.1...v1.2.0) (2026-04-24)

### Features

* **callback:** add ICallbackPlugin + POST /api/plugins/{id}/callback endpoint ([4ec00eb](https://github.com/tgiachi/Arrr/commit/4ec00eb5c8b157f42b1fbaa553244170db2637b5))
* **config-schema:** expose field descriptions in config API and UI ([f7e7e3a](https://github.com/tgiachi/Arrr/commit/f7e7e3a1de403be051fa99829c6396f83288296d))
* **config:** add plugin config GET/POST endpoints with sensitive field encryption ([8093217](https://github.com/tgiachi/Arrr/commit/809321789b52b5dd815b7980ef836c3fe4b6b6f8))
* **docker:** add Dockerfile and build-docker.sh ([b5e4843](https://github.com/tgiachi/Arrr/commit/b5e4843cfca3d6c0a724ff5a303c86dd4f629d33))
* **qr:** add generic IQrPlugin system for in-UI QR code pairing ([17a6220](https://github.com/tgiachi/Arrr/commit/17a622049e90d636f1c7a31281b9769aca7c443d))
* **telegram:** add TelegramPlugin via MTProto user account (WTelegramClient) ([1994ad4](https://github.com/tgiachi/Arrr/commit/1994ad496ffda449762505113dd16a53e39425f9))
* **ui:** add Vite + React + Chakra UI plugin manager ([be0597b](https://github.com/tgiachi/Arrr/commit/be0597bb7ad2390dbceb30a37a3db4c86fab6fbf))
* **whatsapp:** add WhatsApp plugin via whatsmeow Go bridge ([1a96aa8](https://github.com/tgiachi/Arrr/commit/1a96aa80c1fb542e0cfb7cf0af082a038ea592c7))

### Bug Fixes

* **ci:** create local-packages dir before dotnet restore to avoid NU1301 ([5a3b293](https://github.com/tgiachi/Arrr/commit/5a3b293e4991f01458d885257e890b7c1f055758))
* **config:** scan plugins dir as fallback when dllPaths cache misses ([126a58c](https://github.com/tgiachi/Arrr/commit/126a58cba70656182bbd7a9267973189245bc739))
* **rss:** set User-Agent header to avoid 403 from Reddit and similar feeds ([644fdb5](https://github.com/tgiachi/Arrr/commit/644fdb513362d027583eba537b4032903d370881))
* **telegram:** return null from ConfigCallback for unknown keys ([ab1134d](https://github.com/tgiachi/Arrr/commit/ab1134df0dc366df3c999e121ddb31b64c989769))

## [1.1.0](https://github.com/tgiachi/Arrr/compare/v1.0.0...v1.1.0) (2026-04-24)

### Features

* **core:** add plugin config service with sensitive field encryption ([3d43ba2](https://github.com/tgiachi/Arrr/commit/3d43ba285c1c1a4259acfa8f4b2c10c579d88dca))
* **plugins:** add IPluginManager with enable/disable/reload endpoints ([ad3556e](https://github.com/tgiachi/Arrr/commit/ad3556e17af48a10c082dac4494e94a283a7114f))
* **plugins:** install/uninstall plugins from NuGet + RSS first-poll seeding ([76d925c](https://github.com/tgiachi/Arrr/commit/76d925cc6020436ee178fe14861e08086f3bd0d1))

## 1.0.0 (2026-04-24)

### Features

* **api:** add ExternalNotifyRequest DTO and ApiKey to ArrrConfig ([008b2a5](https://github.com/tgiachi/Arrr/commit/008b2a5b7c4c353cc2115faaa6932ed31b71c53e))
* **api:** add GET /api/plugins endpoint ([c9ee27d](https://github.com/tgiachi/Arrr/commit/c9ee27ddf4905a962d01f32ed92fcef88acb4dbc))
* **api:** implement POST /api/notify external plugin endpoint ([32097a0](https://github.com/tgiachi/Arrr/commit/32097a076079433e4124d8d175ac18bd12762e77))
* **assets:** add pirate logo and include it in NuGet package ([f6fc2ff](https://github.com/tgiachi/Arrr/commit/f6fc2ff850976a9457cbedbccb9bf3ffe00e6d59))
* **ci:** add semantic-release with changelog generation ([5bdfb92](https://github.com/tgiachi/Arrr/commit/5bdfb9277d7d76d770899d3e943dcc408b72ffcf))
* **ci:** pack and push Arrr.Templates alongside Arrr.Core on tag ([f81c960](https://github.com/tgiachi/Arrr/commit/f81c9603507d3ae3fa494c8462d8154f0bf1abc3))
* **config:** add IConfigService and ConfigService with arrr.config auto-creation ([8703b51](https://github.com/tgiachi/Arrr/commit/8703b51dd3bb5f1d0631d11bea98883dc975a808))
* **core:** add ArrrWebConfig with Port; wire HTTP port from config in Program.cs ([04822a7](https://github.com/tgiachi/Arrr/commit/04822a724345f1d593e09c1d8211769480ec9f09))
* **core:** add IArrrEvent interface and make Notification implement it ([1bb271e](https://github.com/tgiachi/Arrr/commit/1bb271e09a724d04eb2a5774b2a026d7267cba14))
* **core:** add IEventBus, IPluginContext, IPluginRegistry; extend ISourcePlugin with metadata ([01868a6](https://github.com/tgiachi/Arrr/commit/01868a63d5e31e7c64938beec314ed81029c3f24))
* **core:** add PluginEntry, update ArrrConfig with HttpPort and Plugins, register PluginEntry in JsonContext ([8cce1b1](https://github.com/tgiachi/Arrr/commit/8cce1b15fc81c6cabd76a88e9eb4c4f8ae04eb08))
* **core:** implement EventBusService with Channel-based dispatch loop ([b5c0061](https://github.com/tgiachi/Arrr/commit/b5c0061853741a7c34e7414238eef877cea87e07))
* **dbus:** add Tmds.DBus 0.92.0 package and INotifications interface ([ad0e6f6](https://github.com/tgiachi/Arrr/commit/ad0e6f6971dfd37b70053a7023bd7e4eb467e074))
* **dbus:** implement DBusNotifySubscriber IHostedService ([48fa781](https://github.com/tgiachi/Arrr/commit/48fa78110d744cf8ee67b425b62ba2574262710d))
* **dbus:** register DBusNotifySubscriber in service host ([a8f03c9](https://github.com/tgiachi/Arrr/commit/a8f03c984b78c52be4b3dc309b2f7b8220f7dd5f))
* **debug:** add IsDebug flag to ArrrConfig — exposes OpenAPI and Scalar UI ([f16510f](https://github.com/tgiachi/Arrr/commit/f16510fd1a52526cb708982619e0b30ea73c1dfc))
* **deploy:** add systemd user service and self-contained publish config ([dea249a](https://github.com/tgiachi/Arrr/commit/dea249a5a4a3ae7358328ba7de105d3c54b4feac))
* **events:** publish ArrStartedEvent on service startup ([7c537b2](https://github.com/tgiachi/Arrr/commit/7c537b24317a28a9a50d03169781d3d570a4db9a))
* **nuget:** add NuGet metadata, README and publish GitHub Action ([8309a47](https://github.com/tgiachi/Arrr/commit/8309a47da3c807997ea732dea6f14a56d88ab8d4))
* **packaging:** add nfpm config and build_package.sh script ([c61c102](https://github.com/tgiachi/Arrr/commit/c61c10255888e1ea223faab2ee830dc9287fb4b0))
* **packaging:** add RPM package target ([3d91534](https://github.com/tgiachi/Arrr/commit/3d9153493694b8cd0c8eee6c1ae794e7d1ad7b81))
* **plugins:** add IPollingPlugin interface with centralised polling loop ([47a3df8](https://github.com/tgiachi/Arrr/commit/47a3df8bc36b42d08a5537523d898daa8c77f5a9))
* **service:** add EventBusHostedService as IHostedService wrapper for EventBusService ([7311a3a](https://github.com/tgiachi/Arrr/commit/7311a3af055726d28f3278205ba70e7d00c685ba))
* **service:** add IHttpCallbackPlugin, PluginRegistryService; add AspNetCore framework ref; update FakeSourcePlugin for new ISourcePlugin contract ([1f1a8d3](https://github.com/tgiachi/Arrr/commit/1f1a8d37cf1870fd6be40c434e2ab4cc17d091a7))
* **service:** add PluginLoadContext, PluginHost, PluginContextFactory with per-plugin logging and config path ([39d1853](https://github.com/tgiachi/Arrr/commit/39d18532d63b3e678f68bd8e63501cebffc35f8f))
* **service:** add PluginOrchestrator with FileSystemWatcher hot-reload and AssemblyLoadContext isolation ([cc640b2](https://github.com/tgiachi/Arrr/commit/cc640b2e6d26b51cc40875ef54285b34fb0c7fa0))
* **service:** add SocketBroadcastSubscriber, expose BroadcastAsync on UnixSocketServer; remove ChannelReader from constructor ([b20eea7](https://github.com/tgiachi/Arrr/commit/b20eea7afda04d3b00724456dcf56bd0e25b217d))
* **service:** migrate to WebApplication with SDK.Web, wire all services, expose /callback/{pluginName} endpoints ([0df1307](https://github.com/tgiachi/Arrr/commit/0df130711443cef7147aa38964eea8b909251acb))
* **startup:** default root directory to XDG_DATA_HOME/arrr ([96c972d](https://github.com/tgiachi/Arrr/commit/96c972d26c05b7bc8aa36777e7c4e7fbf4de17b0))
* **templates:** add Arrr.Templates NuGet project skeleton ([6d35231](https://github.com/tgiachi/Arrr/commit/6d352317eb098d23853298ae4a9dcfed175a9aff))
* **templates:** add generated project README with deploy instructions ([fd77f93](https://github.com/tgiachi/Arrr/commit/fd77f93a6a9e6fd526403c572740b9f1bea19b17))
* **templates:** add IPollingPlugin skeleton with symbol substitutions ([c5fca70](https://github.com/tgiachi/Arrr/commit/c5fca70c2900f178fe65fbbcae66fbaace294353))
* **templates:** add template csproj referencing Arrr.Core NuGet ([1feb028](https://github.com/tgiachi/Arrr/commit/1feb028332b24a5d6ecc874d0f0ca39a1787b58d))
* **templates:** add template.json with PluginId, Author, Interval params ([2f6f2af](https://github.com/tgiachi/Arrr/commit/2f6f2afad586e0970c633da9694df0c681293091))
* **tests:** add Arrr.Tests project with NUnit test suite ([55b93e2](https://github.com/tgiachi/Arrr/commit/55b93e2491f26c643c05ab2ffaa380f7324943b4))

### Bug Fixes

* **packaging:** add allow-loopback-pinentry and fix export step ([d920d4d](https://github.com/tgiachi/Arrr/commit/d920d4dbc66c0f0a3ce54c0a352982e7294b7e0d))
* **packaging:** fix GPG signing — export key to temp file at build time ([60b3f9d](https://github.com/tgiachi/Arrr/commit/60b3f9dc3cbf0e0adcaa5af4c6c61616c609eac0))
* **packaging:** pass GPG passphrase explicitly in nfpm.yaml signature config ([5b98442](https://github.com/tgiachi/Arrr/commit/5b984429c58f02af2389c8677cda7ce6f2189f73))
* **packaging:** prompt for GPG passphrase via NFPM_DEB_SIGNING_KEY_PASSPHRASE ([e10ed23](https://github.com/tgiachi/Arrr/commit/e10ed23200e2f85159be1a63deff595ffe7c245b))
* **packaging:** strip GPG passphrase before handing key to nfpm ([e4f4fb7](https://github.com/tgiachi/Arrr/commit/e4f4fb74ca5f71b8f1550a062aa8f2ef0da28910))
* **packaging:** use correct nfpm env var NFPM_DEB_PASSPHRASE ([271c22e](https://github.com/tgiachi/Arrr/commit/271c22e54cebc3f964a60f76a45c8423a274cc53))
* **templates:** add TargetFramework, exclude template files from compile, suppress NU5128 ([59318e6](https://github.com/tgiachi/Arrr/commit/59318e652194baa44617a58577e5d35c22b89414))
