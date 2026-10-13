namespace SesPlus;

internal static class LegalStrings
{
    private static readonly string[] Codes = ["en", "tr", "es", "fr", "pt", "id", "de"];
    private static readonly Dictionary<string, string[]> Strings = new()
    {
        ["licenses"] = ["Licenses", "Lisanslar", "Licencias", "Licences", "Licenças", "Lisensi", "Lizenzen"],
        ["title"] = ["Licenses and runtime terms", "Lisanslar ve çalışma zamanı koşulları", "Licencias y condiciones del runtime", "Licences et conditions du runtime", "Licenças e termos do runtime", "Lisensi dan ketentuan runtime", "Lizenzen und Laufzeitbedingungen"],
        ["intro"] = [
            "Volume + source code is MIT licensed. This Windows build also includes Microsoft components with separate terms. Read the complete terms below before accepting. Acceptance applies to those components; your MIT rights to the Volume + source remain unchanged.",
            "Volume + kaynak kodu MIT lisanslıdır. Bu Windows sürümü ayrıca ayrı koşullara tabi Microsoft bileşenleri içerir. Kabul etmeden önce aşağıdaki tam metinleri okuyun. Onay bu bileşenler içindir; Volume + kaynak kodundaki MIT haklarınız korunur.",
            "El código de Volume + tiene licencia MIT. Esta versión de Windows incluye componentes de Microsoft con condiciones propias. Lea los textos completos antes de aceptar. La aceptación afecta a esos componentes; conserva sus derechos MIT sobre Volume +.",
            "Le code source de Volume + est sous licence MIT. Cette version Windows contient aussi des composants Microsoft soumis à des conditions distinctes. Lisez les textes complets avant d'accepter. Vos droits MIT sur le code de Volume + restent inchangés.",
            "O código do Volume + tem licença MIT. Esta versão para Windows também inclui componentes da Microsoft com termos próprios. Leia os textos completos antes de aceitar. A aceitação se aplica a esses componentes; seus direitos MIT sobre o Volume + permanecem.",
            "Kode sumber Volume + berlisensi MIT. Versi Windows ini juga menyertakan komponen Microsoft dengan ketentuan terpisah. Baca teks lengkap sebelum menyetujui. Persetujuan berlaku untuk komponen tersebut; hak MIT atas kode Volume + tetap berlaku.",
            "Der Volume +-Quellcode steht unter der MIT-Lizenz. Diese Windows-Version enthält auch Microsoft-Komponenten mit eigenen Bedingungen. Lies die vollständigen Texte vor dem Zustimmen. Deine MIT-Rechte am Volume +-Quellcode bleiben bestehen."
        ],
        ["agree"] = [
            "I agree to the included .NET Library and Windows SDK license terms for the bundled Microsoft components.",
            "Paketlenen Microsoft bileşenleri için ekli .NET Library ve Windows SDK lisans koşullarını kabul ediyorum.",
            "Acepto las condiciones de las licencias .NET Library y Windows SDK para los componentes de Microsoft incluidos.",
            "J'accepte les conditions .NET Library et Windows SDK pour les composants Microsoft inclus.",
            "Aceito os termos das licenças .NET Library e Windows SDK para os componentes Microsoft incluídos.",
            "Saya menyetujui ketentuan lisensi .NET Library dan Windows SDK untuk komponen Microsoft yang disertakan.",
            "Ich stimme den enthaltenen .NET Library- und Windows SDK-Lizenzbedingungen für die Microsoft-Komponenten zu."
        ],
        ["accept"] = ["Accept and continue", "Kabul et ve devam et", "Aceptar y continuar", "Accepter et continuer", "Aceitar e continuar", "Setujui dan lanjutkan", "Zustimmen und fortfahren"],
        ["decline"] = ["Decline and exit", "Reddet ve çık", "Rechazar y salir", "Refuser et quitter", "Recusar e sair", "Tolak dan keluar", "Ablehnen und beenden"],
        ["close"] = ["Close", "Kapat", "Cerrar", "Fermer", "Fechar", "Tutup", "Schließen"],
        ["source"] = ["Volume + MIT", "Volume + MIT", "Volume + MIT", "Volume + MIT", "Volume + MIT", "Volume + MIT", "Volume + MIT"],
        ["notices"] = ["Third-party notices", "Üçüncü taraf bildirimleri", "Avisos de terceros", "Avis de tiers", "Avisos de terceiros", "Pemberitahuan pihak ketiga", "Drittanbieterhinweise"],
        ["saveFailure"] = [
            "The license acceptance could not be saved. Volume + has not started: {0}",
            "Lisans onayı kaydedilemedi. Volume + başlatılmadı: {0}",
            "No se pudo guardar la aceptación. Volume + no se ha iniciado: {0}",
            "Impossible d'enregistrer l'acceptation. Volume + n'a pas démarré : {0}",
            "Não foi possível salvar a aceitação. O Volume + não foi iniciado: {0}",
            "Persetujuan tidak dapat disimpan. Volume + belum dimulai: {0}",
            "Die Zustimmung konnte nicht gespeichert werden. Volume + wurde nicht gestartet: {0}"
        ]
    };

    public static string Get(string key, string language) => Strings[key][Array.IndexOf(Codes, Localization.NormalizeLanguage(language))];

    public static void Validate()
    {
        foreach (var (key, translations) in Strings)
            if (translations.Length != Codes.Length || translations.Any(string.IsNullOrWhiteSpace))
                throw new InvalidOperationException("A legal interface translation is missing: " + key);
    }
}
