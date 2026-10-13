# Volume + privacy

This notice describes Volume + 1.2.4.

Volume + contains no app analytics service, sign-in, advertising, or automatic upload to the project authors. It enumerates Windows audio output devices locally and edits the configuration of the separately installed Equalizer APO. It does not record or upload audio.

## Local files

Volume + saves your selected interface language, output device identifier, and percentage in `%LOCALAPPDATA%\SesPlus\preferences.json`. It also stores a local hash of the runtime-license terms you accepted, so the app can ask again if those terms change. These records are not uploaded by Volume +.

The app writes its managed `sesplus.txt` file and a marked include block in Equalizer APO's configuration folder. Backups of `config.txt` are also kept in that folder before relevant changes. These files may contain your local audio configuration and device identifiers. They remain on your device; avoid posting them publicly without reviewing their contents.

The `SesPlus` folder and Equalizer APO integration names are retained for compatibility with earlier versions of the app. Renaming the app to Volume + does not move or upload these local records.

Close Volume + before removing its preference or acceptance records. Removing acceptance records causes the terms prompt to appear again. Use **Remove Volume + from audio config** to remove the app's managed integration from Equalizer APO.

## Browser links

The optional Equalizer APO download link opens the official project page in your browser. The browser and destination site then operate under their own settings and privacy policies.

## Windows and Microsoft components

The self-contained release includes Microsoft .NET and Windows Desktop runtime components. Their included license terms describe possible Microsoft data collection. Windows and .NET error-reporting facilities may send diagnostic or crash data to Microsoft according to your Windows configuration and organizational policies. Volume + does not use a project-operated crash-reporting endpoint.

See the [Microsoft Privacy Statement](https://privacy.microsoft.com/privacystatement) and [Windows Error Reporting documentation](https://learn.microsoft.com/en-us/windows/win32/wer/windows-error-reporting) for the relevant Microsoft controls. The build script's .NET CLI telemetry opt-out applies to build tooling; it does not change Windows runtime diagnostic settings.

---

## Türkçe

Volume + analitik hizmeti, kullanıcı hesabı, reklam veya proje sahiplerine otomatik veri yükleme içermez. Ses kaydetmez ya da yüklemez. Dil, çıkış cihazı, yüzde ve çalışma zamanı lisans onayı cihazınızda saklanır. Equalizer APO yapılandırması ve yedekleri de cihazınızda kalır.

İsteğe bağlı indirme bağlantısı tarayıcıda resmi Equalizer APO sayfasını açar. Windows ve .NET hata raporlaması, Windows ayarları ve kurum politikalarına göre Microsoft'a tanılama verisi gönderebilir. Bu davranış için [Microsoft gizlilik bildirimi](https://privacy.microsoft.com/privacystatement) geçerlidir. Derleme araçlarının telemetri ayarı Windows'un çalışma zamanı tanılama ayarlarını değiştirmez.
