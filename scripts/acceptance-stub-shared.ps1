# 受入の実行器を確かめる2つの相手——操作役と応答を作る相手——が共有する値。
# 前置が2つへ渡し、どちらも同じ並びと大きさを前提にする。

# 起動したことにするエディタの、1つ目のプロセスID。2つ目以降は1ずつ増える。
$FirstStubEditorId = 101

# 写したことにするビューの大きさ。返す絵もこの大きさにする。
$StubViewWidth = 64
$StubViewHeight = 48

# 何回目の起動かを持ち越す置き場の名前。前置が走るたびに捨てる。
$StubLaunchStateName = "pmx-editor-mcp-acceptance-stub-launched.txt"

# 段をどこまで進めたかを持ち越す置き場の名前。サーバーを起こし直す段をまたぐのに要る。
$StubProgressStateName = "pmx-editor-mcp-acceptance-stub-progress.json"

# 頼まれた操作を書き留める置き場の名前。実行器が操作の段をこなしたかを、ここで外から確かめる。
$StubOperationLogName = "pmx-editor-mcp-acceptance-stub-operations.txt"
