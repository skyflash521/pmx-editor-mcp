# 実行器を確かめる題材——操作役と応答を作る相手——が共有する値。
# 前置が題材へ渡し、どれも同じ並びと大きさを前提にする。

# 起動したことにするエディタの、1つ目のプロセスID。2つ目以降は1ずつ増える。
$FirstStubEditorId = 101

# 写したことにするビューの大きさ。返す画像もこの大きさにする。
$StubViewWidth = 64
$StubViewHeight = 48

# 何回目の起動かを持ち越す置き場の名前。前置が走るたびに捨てる。
$StubLaunchStateName = "pmx-editor-mcp-acceptance-stub-launched.txt"

# 段をどこまで進めたかを持ち越す置き場の名前。サーバーを起こし直す段をまたぐのに要る。
$StubProgressStateName = "pmx-editor-mcp-acceptance-stub-progress.json"

# 頼まれた操作を書き留める置き場の名前。実行器が操作の段をこなしたかを、ここで外から確かめる。
$StubOperationLogName = "pmx-editor-mcp-acceptance-stub-operations.txt"

# 起こしたことにするエディタを、ブリッジと参照クライアントの題材が持ち越す置き場の名前。
$LiveStubEditorsName = "pmx-editor-mcp-live-stub-editors.txt"

# 何回目の起動かを、同じ題材が持ち越す置き場の名前。
$LiveStubLaunchedName = "pmx-editor-mcp-live-stub-launched.txt"

# 実機動作確認の題材が開く待受の、名前の付け方。ホスト側の実装と同じ形にする。
$LiveHostStubPipePrefix = "pmx-editor-mcp-"

# 起こしたことにするエディタの、状態を持ち越す置き場の名前の書き出し。プロセスIDを後ろへ足す。
$LiveHostStubStatePrefix = "pmx-editor-mcp-livehost-stub-"

# 状態の綴り。待受を開けているか、閉じたか、終わるかを表す。
$LiveHostStubRunning = "running"
$LiveHostStubStopped = "stopped"
$LiveHostStubClosed = "closed"

# 題材が違える形を伝える環境変数。操作役・待受・確認クライアントの代わりが同じ値を読む。
$LiveHostStubBrokenName = "PMX_EDITOR_MCP_STUB_BROKEN"

# 待受を残す違え方が、1つ目の停止でだけ効くようにする印の置き場の名前。停止のたびに待受を残すと、
# 切断を待つ側が待ちきれるまで実行が止まり、1回ぶんがその待ちのぶんだけ伸びる。
$LiveHostStubIgnoredName = "pmx-editor-mcp-livehost-stub-ignored.txt"

# 題材が、状態の変わるのを諦めるまでの秒数。実物の操作役の既定と同じ値を採る。
$LiveStubWaitSeconds = 40
