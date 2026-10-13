using System.Globalization;
using System.Text.RegularExpressions;

namespace SesPlus;

internal sealed record LanguageOption(string Code, string NativeName, string CultureName)
{
    public override string ToString() => NativeName;
}

internal static class Localization
{
    public static IReadOnlyList<LanguageOption> SupportedLanguages { get; } = Array.AsReadOnly(new[]
    {
        new LanguageOption("en", "English", "en-US"),
        new LanguageOption("tr", "Türkçe", "tr-TR"),
        new LanguageOption("es", "Español", "es-ES"),
        new LanguageOption("fr", "Français", "fr-FR"),
        new LanguageOption("pt", "Português (Brasil)", "pt-BR"),
        new LanguageOption("id", "Bahasa Indonesia", "id-ID"),
        new LanguageOption("de", "Deutsch", "de-DE")
    });

    // Each row follows the order in SupportedLanguages. Keeping all translations
    // together makes missing translations visible and prevents English fallbacks.
    private static readonly Dictionary<string, string[]> Strings = new()
    {
        ["title"] = ["Volume + · Volume Booster", "Volume + · Ses Yükseltici", "Volume + · Amplificador de volumen", "Volume + · Amplificateur de volume", "Volume + · Amplificador de volume", "Volume + · Penguat Volume", "Volume + · Lautstärkeverstärker"],
        ["brand"] = ["SYSTEM VOLUME BOOSTER", "SİSTEM SESİ YÜKSELTİCİ", "AMPLIFICADOR DE VOLUMEN DEL SISTEMA", "AMPLIFICATEUR DU VOLUME SYSTÈME", "AMPLIFICADOR DE VOLUME DO SISTEMA", "PENGUAT VOLUME SISTEM", "SYSTEMLAUTSTÄRKE VERSTÄRKEN"],
        ["language"] = ["Language", "Dil", "Idioma", "Langue", "Idioma", "Bahasa", "Sprache"],
        ["gain"] = ["VOLUME GAIN", "SES KAZANCI", "AMPLIFICACIÓN", "AMPLIFICATION", "AMPLIFICAÇÃO", "PENGUATAN VOLUME", "VERSTÄRKUNG"],
        ["hero"] = ["A little more volume.", "Biraz daha yüksek ses.", "Un poco más de volumen.", "Un peu plus de volume.", "Um pouco mais de volume.", "Sedikit lebih kencang.", "Ein bisschen lauter."],
        ["subtitle"] = ["Give quiet audio the extra lift it needs.", "Düşük sesli içeriklere ihtiyacı olan gücü ver.", "Dale al audio bajo el impulso que necesita.", "Donnez aux sons faibles le coup de pouce nécessaire.", "Dê ao áudio baixo o reforço que ele precisa.", "Beri suara pelan penguatan yang dibutuhkan.", "Gib leisem Audio die nötige Verstärkung."],
        ["amplification"] = ["additional gain", "ek kazanç", "amplificación adicional", "amplification supplémentaire", "amplificação adicional", "penguatan tambahan", "zusätzliche Verstärkung"],
        ["normal"] = ["Normal", "Normal", "Normal", "Normal", "Normal", "Normal", "Normal"],
        ["apply"] = ["Apply boost", "Kazancı uygula", "Aplicar amplificación", "Appliquer", "Aplicar amplificação", "Terapkan penguatan", "Verstärkung anwenden"],
        ["pause"] = ["Turn boost off", "Kazancı kapat", "Desactivar amplificación", "Désactiver", "Desativar amplificação", "Matikan penguatan", "Verstärkung ausschalten"],
        ["resume"] = ["Turn boost on", "Kazancı aç", "Activar amplificación", "Activer", "Ativar amplificação", "Aktifkan penguatan", "Verstärkung einschalten"],
        ["reset"] = ["Reset to 100%", "%100'e dön", "Volver al 100%", "Revenir à 100 %", "Voltar a 100%", "Kembali ke 100%", "Auf 100 % zurücksetzen"],
        ["customButton"] = ["Custom percentage…", "Yüzde gir…", "Porcentaje personalizado…", "Pourcentage personnalisé…", "Porcentagem personalizada…", "Persentase khusus…", "Eigener Prozentwert…"],
        ["customTitle"] = ["Choose your percentage", "İstediğin yüzdeyi gir", "Elige tu porcentaje", "Choisissez votre pourcentage", "Escolha sua porcentagem", "Pilih persentase Anda", "Wähle deinen Prozentwert"],
        ["customDescription"] = ["Enter a whole number above 0. The normal slider stays at 100-200%.", "0'dan büyük bir tam sayı gir. Normal kaydırıcı 100-200% aralığında kalır.", "Introduce un número entero mayor que 0. El control normal se mantiene entre el 100-200%.", "Saisissez un nombre entier supérieur à 0. Le curseur normal reste entre 100-200 %.", "Digite um número inteiro maior que 0. O controle normal permanece entre 100-200%.", "Masukkan bilangan bulat di atas 0. Slider normal tetap pada 100-200%.", "Gib eine ganze Zahl über 0 ein. Der normale Regler bleibt bei 100-200 %."],
        ["customValueLabel"] = ["Volume level (%)", "Ses seviyesi (%)", "Nivel de volumen (%)", "Niveau de volume (%)", "Nível de volume (%)", "Level volume (%)", "Lautstärke (%)"],
        ["customWarningTitle"] = ["Heads up: extreme volume", "Dikkat: aşırı yüksek ses", "Ojo: volumen extremo", "Attention : volume extrême", "Atenção: volume extremo", "Perhatian: volume ekstrem", "Achtung: extreme Lautstärke"],
        ["customWarning"] = ["Even 200% is risky. Higher levels can seriously mess up the sound!", "%200 bile tehlikeli. Daha fazlası sesi baya bok eder haaaa!", "Incluso el 200% es arriesgado. ¡Un nivel más alto puede estropear mucho el sonido!", "Même 200 % est risqué. Un niveau plus élevé peut vraiment dégrader le son !", "Até 200% já é arriscado. Mais do que isso pode estragar bastante o som!", "200% saja sudah berisiko. Level yang lebih tinggi bisa bikin suara sangat kacau!", "Schon 200 % ist riskant. Höhere Werte können den Klang ordentlich ruinieren!"],
        ["customAcknowledge"] = ["I understand that the sound may become badly distorted.", "Sesin ciddi biçimde bozulabileceğini anlıyorum.", "Entiendo que el sonido puede distorsionarse mucho.", "Je comprends que le son peut être fortement déformé.", "Entendo que o som pode ficar muito distorcido.", "Saya memahami bahwa suara bisa sangat terdistorsi.", "Ich verstehe, dass der Klang stark verzerrt werden kann."],
        ["customApply"] = ["Apply percentage", "Yüzdeyi uygula", "Aplicar porcentaje", "Appliquer le pourcentage", "Aplicar porcentagem", "Terapkan persentase", "Prozentwert anwenden"],
        ["cancel"] = ["Cancel", "İptal", "Cancelar", "Annuler", "Cancelar", "Batal", "Abbrechen"],
        ["customInvalid"] = ["Enter a valid positive whole number.", "Geçerli, pozitif bir tam sayı gir.", "Introduce un número entero positivo válido.", "Saisissez un nombre entier positif valide.", "Digite um número inteiro positivo válido.", "Masukkan bilangan bulat positif yang valid.", "Gib eine gültige positive ganze Zahl ein."],
        ["customSliderNote"] = ["Custom level: {0}. Use the slider or presets to return to 100-200%.", "Özel seviye: {0}. 100-200% aralığına dönmek için kaydırıcıyı veya hazır seviyeleri kullan.", "Nivel personalizado: {0}. Usa el control o los niveles predefinidos para volver al 100-200%.", "Niveau personnalisé : {0}. Utilisez le curseur ou les préréglages pour revenir à 100-200 %.", "Nível personalizado: {0}. Use o controle ou as predefinições para voltar a 100-200%.", "Level khusus: {0}. Gunakan slider atau preset untuk kembali ke 100-200%.", "Eigener Pegel: {0}. Verwende den Regler oder die Vorgaben, um zu 100-200 % zurückzukehren."],
        ["autoResetNote"] = ["Closing Volume + restores 100%.", "Volume + kapanınca ses %100'e döner.", "Al cerrar Volume +, el nivel vuelve al 100%.", "Fermer Volume + rétablit le niveau à 100 %.", "Ao fechar o Volume +, o nível volta a 100%.", "Menutup Volume + mengembalikan level ke 100%.", "Beim Schließen setzt Volume + den Pegel auf 100 % zurück."],
        ["output"] = ["Your audio path", "Ses çıkışın", "Tu salida de audio", "Votre sortie audio", "Sua saída de áudio", "Output audio Anda", "Deine Audioausgabe"],
        ["device"] = ["OUTPUT DEVICE", "ÇIKIŞ CİHAZI", "DISPOSITIVO DE SALIDA", "PÉRIPHÉRIQUE DE SORTIE", "DISPOSITIVO DE SAÍDA", "PERANGKAT OUTPUT", "AUSGABEGERÄT"],
        ["deviceHelp"] = ["Choose the same device in Equalizer APO's Device Selector.", "Equalizer APO Cihaz Seçici'de aynı cihazı seç.", "Elige el mismo dispositivo en Device Selector de Equalizer APO.", "Sélectionnez le même périphérique dans Device Selector d'Equalizer APO.", "Escolha o mesmo dispositivo no Device Selector do Equalizer APO.", "Pilih perangkat yang sama di Device Selector Equalizer APO.", "Wähle dasselbe Gerät im Device Selector von Equalizer APO."],
        ["installed"] = ["Found", "Bulundu", "Detectado", "Détecté", "Encontrado", "Ditemukan", "Gefunden"],
        ["missing"] = ["Not installed", "Kurulu değil", "Sin instalar", "Non installé", "Não instalado", "Belum terpasang", "Nicht installiert"],
        ["setupIntro"] = ["One-time setup for real system-wide amplification.", "Gerçek sistem sesi yükseltme için bir kerelik kurulum.", "Una única instalación para amplificar todo el audio del sistema.", "Une installation unique pour amplifier le son de tout le système.", "Uma configuração inicial para amplificar todo o áudio do sistema.", "Pengaturan satu kali untuk memperkuat suara seluruh sistem.", "Einmal einrichten, um den Ton im gesamten System zu verstärken."],
        ["stepOne"] = ["01  Install the audio engine.", "01  Ses motorunu kur.", "01  Instala el motor de audio.", "01  Installez le moteur audio.", "01  Instale o mecanismo de áudio.", "01  Pasang mesin audio.", "01  Installiere die Audio-Engine."],
        ["download"] = ["Get Equalizer APO ↗", "Equalizer APO'yu indir ↗", "Descargar Equalizer APO ↗", "Télécharger Equalizer APO ↗", "Baixar Equalizer APO ↗", "Unduh Equalizer APO ↗", "Equalizer APO herunterladen ↗"],
        ["stepTwo"] = ["02  Enable your output device.", "02  Çıkış cihazını etkinleştir.", "02  Activa tu dispositivo de salida.", "02  Activez votre périphérique de sortie.", "02  Ative seu dispositivo de saída.", "02  Aktifkan perangkat output Anda.", "02  Aktiviere dein Ausgabegerät."],
        ["selector"] = ["Open Device Selector", "Cihaz Seçici'yi aç", "Abrir Device Selector", "Ouvrir Device Selector", "Abrir Device Selector", "Buka Device Selector", "Device Selector öffnen"],
        ["stepThree"] = ["03  Restart if asked, then refresh.", "03  İstenirse yeniden başlat, sonra yenile.", "03  Reinicia si se solicita y luego actualiza.", "03  Redémarrez si demandé, puis actualisez.", "03  Reinicie se solicitado e atualize.", "03  Mulai ulang jika diminta, lalu segarkan.", "03  Starte bei Bedarf neu und aktualisiere."],
        ["refresh"] = ["Refresh connection", "Bağlantıyı yenile", "Actualizar conexión", "Actualiser la connexion", "Atualizar conexão", "Segarkan koneksi", "Verbindung aktualisieren"],
        ["admin"] = ["Relaunch as administrator", "Yönetici olarak yeniden aç", "Reabrir como administrador", "Relancer en administrateur", "Reabrir como administrador", "Buka ulang sebagai administrator", "Als Administrator neu starten"],
        ["disconnect"] = ["Remove Volume + from audio config", "Volume +'ı ses ayarlarından kaldır", "Quitar Volume + de la configuración de audio", "Retirer Volume + de la configuration audio", "Remover Volume + da configuração de áudio", "Hapus Volume + dari konfigurasi audio", "Volume + aus der Audiokonfiguration entfernen"],
        ["footer"] = ["Start at 125%. Loud peaks may distort. Keep Windows audio enhancements enabled.", "%125 ile başla. Yüksek sesler bozulabilir. Windows ses geliştirmelerini açık tut.", "Empieza al 125%. Los picos altos pueden distorsionarse. Mantén activadas las mejoras de audio de Windows.", "Commencez à 125 %. Les pics sonores peuvent saturer. Gardez les améliorations audio de Windows activées.", "Comece em 125%. Picos altos podem distorcer. Mantenha os aprimoramentos de áudio do Windows ativados.", "Mulai dari 125%. Puncak suara bisa terdistorsi. Biarkan peningkatan audio Windows aktif.", "Beginne mit 125 %. Lautstärkespitzen können verzerren. Lass die Audioverbesserungen von Windows eingeschaltet."],
        ["setupTitle"] = ["Audio engine needed", "Ses motoru gerekiyor", "Se necesita el motor de audio", "Moteur audio nécessaire", "Mecanismo de áudio necessário", "Mesin audio diperlukan", "Audio-Engine erforderlich"],
        ["setupDetail"] = ["Install Equalizer APO using the steps on the right. Your chosen level is ready to apply after setup.", "Sağdaki adımlarla Equalizer APO'yu kur. Seçtiğin seviye kurulumdan sonra uygulanabilir.", "Instala Equalizer APO siguiendo los pasos de la derecha. Después podrás aplicar el nivel elegido.", "Installez Equalizer APO en suivant les étapes à droite. Vous pourrez ensuite appliquer le niveau choisi.", "Instale o Equalizer APO seguindo os passos à direita. Depois, você poderá aplicar o nível escolhido.", "Pasang Equalizer APO dengan langkah di sebelah kanan. Setelah selesai, level pilihan Anda dapat diterapkan.", "Installiere Equalizer APO mit den Schritten rechts. Danach kannst du den gewählten Pegel anwenden."],
        ["readyTitle"] = ["Ready to configure", "Ayarlamaya hazır", "Listo para configurar", "Prêt à configurer", "Pronto para configurar", "Siap dikonfigurasi", "Bereit zum Einrichten"],
        ["readyDetail"] = ["Enable the selected output in Device Selector first. Then apply your preferred gain.", "Önce seçili çıkışı Cihaz Seçici'de etkinleştir. Ardından istediğin kazancı uygula.", "Primero activa la salida elegida en Device Selector. Luego aplica la amplificación que prefieras.", "Activez d'abord la sortie choisie dans Device Selector. Appliquez ensuite l'amplification souhaitée.", "Primeiro, ative a saída escolhida no Device Selector. Depois, aplique a amplificação desejada.", "Aktifkan dulu output yang dipilih di Device Selector. Lalu terapkan penguatan yang diinginkan.", "Aktiviere zuerst die gewählte Ausgabe im Device Selector. Wende dann die gewünschte Verstärkung an."],
        ["savedTitle"] = ["Gain saved · {0}", "Kazanç kaydedildi · {0}", "Amplificación guardada · {0}", "Amplification enregistrée · {0}", "Amplificação salva · {0}", "Penguatan tersimpan · {0}", "Verstärkung gespeichert · {0}"],
        ["savedDetail"] = ["{0} · Changes take effect when Equalizer APO is enabled for this device. Closing Volume + restores 100%.", "{0} · Equalizer APO bu cihazda etkinse değişiklikler uygulanır. Volume + kapanınca ses %100'e döner.", "{0} · Los cambios se aplican si Equalizer APO está activado para este dispositivo. Al cerrar Volume +, el nivel vuelve al 100%.", "{0} · Les modifications s'appliquent si Equalizer APO est activé pour ce périphérique. Fermer Volume + rétablit le niveau à 100 %.", "{0} · As mudanças são aplicadas quando o Equalizer APO está ativo neste dispositivo. Ao fechar o Volume +, o nível volta a 100%.", "{0} · Perubahan berlaku jika Equalizer APO aktif untuk perangkat ini. Menutup Volume + mengembalikan level ke 100%.", "{0} · Änderungen wirken, wenn Equalizer APO für dieses Gerät aktiviert ist. Beim Schließen setzt Volume + den Pegel auf 100 % zurück."],
        ["pausedTitle"] = ["Additional gain is off", "Ek kazanç kapalı", "Amplificación adicional desactivada", "Amplification supplémentaire désactivée", "Amplificação adicional desativada", "Penguatan tambahan mati", "Zusätzliche Verstärkung ausgeschaltet"],
        ["pausedDetail"] = ["Volume + adds 0 dB. Your existing audio configuration still applies.", "Volume + 0 dB ekliyor. Mevcut ses ayarların uygulanmaya devam eder.", "Volume + añade 0 dB. Se mantiene tu configuración de audio actual.", "Volume + ajoute 0 dB. Votre configuration audio existante reste active.", "O Volume + adiciona 0 dB. Sua configuração de áudio atual continua ativa.", "Volume + menambahkan 0 dB. Konfigurasi audio Anda yang ada tetap berlaku.", "Volume + fügt 0 dB hinzu. Deine vorhandene Audiokonfiguration bleibt aktiv."],
        ["removedTitle"] = ["Volume + disconnected", "Volume + bağlantısı kaldırıldı", "Volume + desconectado", "Volume + déconnecté", "Volume + desconectado", "Volume + terputus", "Volume + getrennt"],
        ["removedDetail"] = ["The Volume + integration was removed. Your other audio settings were preserved.", "Volume + bağlantısı kaldırıldı. Diğer ses ayarların korundu.", "Se eliminó la integración de Volume +. Los demás ajustes de audio se conservaron.", "L'intégration de Volume + a été retirée. Vos autres réglages audio ont été conservés.", "A integração do Volume + foi removida. Suas outras configurações de áudio foram preservadas.", "Integrasi Volume + telah dihapus. Pengaturan audio lainnya tetap dipertahankan.", "Die Volume +-Integration wurde entfernt. Deine anderen Audioeinstellungen bleiben erhalten."],
        ["noDeviceTitle"] = ["No output device found", "Çıkış cihazı bulunamadı", "No se encontró un dispositivo de salida", "Aucun périphérique de sortie détecté", "Nenhum dispositivo de saída encontrado", "Perangkat output tidak ditemukan", "Kein Ausgabegerät gefunden"],
        ["noDeviceDetail"] = ["Connect speakers or headphones and refresh the connection.", "Hoparlör veya kulaklık bağla ve bağlantıyı yenile.", "Conecta altavoces o auriculares y actualiza la conexión.", "Branchez des haut-parleurs ou un casque, puis actualisez la connexion.", "Conecte alto-falantes ou fones de ouvido e atualize a conexão.", "Hubungkan speaker atau headphone, lalu segarkan koneksi.", "Schließe Lautsprecher oder Kopfhörer an und aktualisiere die Verbindung."],
        ["errorTitle"] = ["Couldn't complete the action", "İşlem tamamlanamadı", "No se pudo completar la acción", "Impossible de terminer l'action", "Não foi possível concluir a ação", "Tindakan tidak dapat diselesaikan", "Aktion konnte nicht abgeschlossen werden"],
        ["errorDetail"] = ["Refresh the connection and try again. See details for the reason.", "Bağlantıyı yenileyip tekrar dene. Nedeni için ayrıntılara bak.", "Actualiza la conexión e inténtalo de nuevo. Consulta los detalles para conocer el motivo.", "Actualisez la connexion et réessayez. Consultez les détails pour connaître la cause.", "Atualize a conexão e tente novamente. Veja os detalhes para saber o motivo.", "Segarkan koneksi dan coba lagi. Lihat detail untuk mengetahui penyebabnya.", "Aktualisiere die Verbindung und versuche es erneut. Den Grund findest du in den Details."],
        ["details"] = ["View details", "Ayrıntıları göster", "Ver detalles", "Voir les détails", "Ver detalhes", "Lihat detail", "Details anzeigen"],
        ["permission"] = ["This config folder needs administrator access. Use the button on the right.", "Bu ayar klasörü yönetici izni istiyor. Sağdaki düğmeyi kullan.", "Esta carpeta de configuración requiere permisos de administrador. Usa el botón de la derecha.", "Ce dossier de configuration exige les droits d'administrateur. Utilisez le bouton à droite.", "Esta pasta de configuração exige acesso de administrador. Use o botão à direita.", "Folder konfigurasi ini memerlukan akses administrator. Gunakan tombol di sebelah kanan.", "Dieser Konfigurationsordner benötigt Administratorrechte. Verwende die Schaltfläche rechts."],
        ["pendingTitle"] = ["New level · {0}", "Yeni seviye · {0}", "Nuevo nivel · {0}", "Nouveau niveau · {0}", "Novo nível · {0}", "Level baru · {0}", "Neuer Pegel · {0}"],
        ["pendingDetail"] = ["Press Apply boost to save this level for the selected device.", "Seçili cihaz için bu seviyeyi kaydetmek üzere Kazancı uygula'ya bas.", "Pulsa Aplicar amplificación para guardar este nivel para el dispositivo elegido.", "Cliquez sur Appliquer pour enregistrer ce niveau pour le périphérique choisi.", "Clique em Aplicar amplificação para salvar este nível para o dispositivo escolhido.", "Tekan Terapkan penguatan untuk menyimpan level ini bagi perangkat yang dipilih.", "Klicke auf Verstärkung anwenden, um diesen Pegel für das gewählte Gerät zu speichern."],
        ["refreshFailure"] = ["Couldn't read the audio devices: {0}", "Ses cihazları okunamadı: {0}", "No se pudieron leer los dispositivos de audio: {0}", "Impossible de lire les périphériques audio : {0}", "Não foi possível ler os dispositivos de áudio: {0}", "Tidak dapat membaca perangkat audio: {0}", "Audiogeräte konnten nicht gelesen werden: {0}"],
        ["launchFailure"] = ["Couldn't open this action: {0}", "Bu işlem açılamadı: {0}", "No se pudo abrir esta acción: {0}", "Impossible de lancer cette action : {0}", "Não foi possível abrir esta ação: {0}", "Tidak dapat membuka tindakan ini: {0}", "Diese Aktion konnte nicht gestartet werden: {0}"],
        ["settingsFailure"] = ["Couldn't save the language preference: {0}", "Dil tercihi kaydedilemedi: {0}", "No se pudo guardar el idioma elegido: {0}", "Impossible d'enregistrer la langue choisie : {0}", "Não foi possível salvar o idioma escolhido: {0}", "Tidak dapat menyimpan pilihan bahasa: {0}", "Die Sprachwahl konnte nicht gespeichert werden: {0}"],
        ["sliderName"] = ["Additional volume gain, 100 to 200 percent", "Ek ses kazancı, yüzde 100 ile 200 arası", "Amplificación adicional del volumen, de 100 a 200 por ciento", "Amplification supplémentaire du volume, de 100 à 200 pour cent", "Amplificação adicional de volume, de 100 a 200 por cento", "Penguatan volume tambahan, 100 hingga 200 persen", "Zusätzliche Lautstärkeverstärkung, 100 bis 200 Prozent"],
        ["alreadyOpen"] = ["Volume + is already running. Use the open window to adjust the volume.", "Volume + zaten çalışıyor. Sesi ayarlamak için açık pencereyi kullan.", "Volume + ya está abierto. Usa la ventana abierta para ajustar el volumen.", "Volume + est déjà ouvert. Utilisez la fenêtre ouverte pour régler le volume.", "O Volume + já está aberto. Use a janela aberta para ajustar o volume.", "Volume + sudah berjalan. Gunakan jendela yang terbuka untuk mengatur volume.", "Volume + läuft bereits. Verwende das geöffnete Fenster, um die Lautstärke anzupassen."],
        ["closeResetFailed"] = ["Couldn't restore 100%. Volume + will stay open so you can try again. Reason: {0}", "%100'e dönülemedi. Tekrar deneyebilmen için Volume + açık kalacak. Neden: {0}", "No se pudo volver al 100%. Volume + seguirá abierto para que puedas intentarlo de nuevo. Motivo: {0}", "Impossible de revenir à 100 %. Volume + reste ouvert pour vous permettre de réessayer. Cause : {0}", "Não foi possível voltar a 100%. O Volume + ficará aberto para você tentar novamente. Motivo: {0}", "Tidak dapat kembali ke 100%. Volume + akan tetap terbuka agar Anda bisa mencoba lagi. Penyebab: {0}", "100 % konnten nicht wiederhergestellt werden. Volume + bleibt geöffnet, damit du es erneut versuchen kannst. Grund: {0}"],
        ["close"] = ["Close", "Kapat", "Cerrar", "Fermer", "Fechar", "Tutup", "Schließen"]
    };

    private static readonly IReadOnlyDictionary<string, int> LanguageIndices = SupportedLanguages
        .Select((language, index) => (language.Code, Index: index))
        .ToDictionary(item => item.Code, item => item.Index, StringComparer.Ordinal);

    static Localization() => Validate();

    public static IReadOnlyCollection<string> Keys => Strings.Keys;

    public static string NormalizeLanguage(string? language)
    {
        var code = language?.Trim().Replace('_', '-').Split('-')[0].ToLowerInvariant();
        return code is not null && LanguageIndices.ContainsKey(code) ? code : "en";
    }

    public static CultureInfo GetCulture(string language) => CultureInfo.GetCultureInfo(
        SupportedLanguages[LanguageIndices[NormalizeLanguage(language)]].CultureName);

    public static string Get(string key, string language) =>
        Strings[key][LanguageIndices[NormalizeLanguage(language)]];

    public static void Validate()
    {
        foreach (var (key, translations) in Strings)
        {
            if (translations.Length != SupportedLanguages.Count)
                throw new InvalidOperationException($"Translation count is invalid for '{key}'.");

            var expectedPlaceholders = Placeholders(translations[0]);
            for (var index = 0; index < translations.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(translations[index]))
                    throw new InvalidOperationException($"Missing '{key}' translation for {SupportedLanguages[index].Code}.");
                if (!expectedPlaceholders.SequenceEqual(Placeholders(translations[index])))
                    throw new InvalidOperationException($"Invalid placeholders in '{key}' for {SupportedLanguages[index].Code}.");
            }
        }
    }

    private static string[] Placeholders(string text) => Regex.Matches(text, @"\{(\d+)(?:,[^}:]+)?(?::[^}]+)?\}")
        .Select(match => match.Groups[1].Value)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
