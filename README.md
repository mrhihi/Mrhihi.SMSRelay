# SMS Relay

以 .NET MAUI 製作的 Android App：依來源或簡訊內容規則，將收到的新 SMS 自動轉發到 Gotify；也能瀏覽並手動批次轉發歷史簡訊。

## 功能

- 自動接收 SMS，支援電話號碼、文字 Sender ID 與簡訊本文規則。
- 規則可選完全相符、前綴或 Regex；新增、啟用/停用、刪除後立即儲存。
- 可設定多台 Gotify HTTPS Server；每台可有多個具名稱的 application token，並可逐一測試。
- 規則可分群組；群組內可混用 AND／OR（AND 優先），每群組可指定多個轉發 Token。
- 本機加密佇列、網路恢復後背景重試、單筆與全部清除紀錄。
- 歷史 SMS 依日期範圍瀏覽、前後跳至有訊息的日期、進階全文搜尋、每批載入 100 筆與批次手動轉發。
- 設定頁使用摘要列表與獨立編輯頁；規則以「任一 OR 條件組／組內 AND 條件」呈現，避免複雜運算子造成誤解。
- 歷史簡訊預設為純瀏覽模式，按「選取」後才出現批次轉發控制項；傳送紀錄會依 SMS 合併顯示各 Token 的結果。

## 使用方式

1. 安裝 APK 後，於首頁授權讀取與接收 SMS。
2. 在「設定」新增 Gotify Server，填入 HTTPS URL 與優先權；在該 Server 下新增具名稱的 application token，並按「測試」。
3. 新增「自動轉發規則」群組，選擇其目的地 Token；再於群組中選擇比對欄位與方式：
   - **發送者（電話／名稱）**：電話號碼會正規化後比對；文字 Sender ID 以原樣比對。
   - **簡訊內容**：對本文套用完全相符、前綴或 Regex。
4. 同一群組的第二條起可指定 AND 或 OR；AND 會優先計算。命中群組的所有目的地都會收到訊息，同一 Token 僅發送一次。
5. 「歷史簡訊」預設顯示當天資料；可選連續 1–10 天、使用今天／上月／下月快捷鍵，或展開進階搜尋進行不限日期的文字搜尋。勾選後可手選一個或多個 Token 加入傳送佇列。

> 歷史 SMS 絕不會自動轉發；只有使用者明確勾選後才會送出。

## 開發與建置

需求：.NET 10 SDK、`maui-android` workload、Android SDK（API 36）與 JDK 21。

```sh
sudo dotnet workload install maui-android
export ANDROID_SDK_ROOT="$HOME/Library/Android/sdk"
export JAVA_HOME="/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home"

make restore
make build
make apk
```

`make` 可列出所有目標與可覆寫參數。`make apk` 會將簽署後的 APK 放在根目錄的 `publish/`：

```sh
adb install publish/net.mrhihi.smsrelay-Signed.apk
```

如安裝後發生舊圖示或 Fast Deployment 殘留問題，先解除安裝再重裝：

```sh
adb uninstall net.mrhihi.smsrelay
adb install publish/net.mrhihi.smsrelay-Signed.apk
```

## 安全與限制

- Gotify token 使用 Android secure storage 保存；佇列內的待送 payload 與目的地快照使用 AES-GCM 加密。
- 待送與失敗 SMS payload 使用 AES-GCM 本機加密；成功後只保留傳送狀態，不保存本文。
- 需要 `READ_SMS` 與 `RECEIVE_SMS` 權限，適合內部側載或受管裝置。SMS 權限在部分 Android 安裝來源與發佈通路受到限制。
- 僅支援 Android、SMS 與 Gotify；不處理 MMS。

## 授權

本專案採用 [MIT License](LICENSE)。
