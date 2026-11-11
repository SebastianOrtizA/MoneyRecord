using MoneyRecord.Helpers;
using MoneyRecord.Models;
using MoneyRecord.Services.Interfaces;
using System.Globalization;

namespace MoneyRecord.Services
{
    public class CurrencyService : ICurrencyService
    {
        /// <summary>Every ISO 4217 currency in use, sorted by code (the pickers show the code first).</summary>
        private static readonly List<Currency> _currencies = new List<Currency>
        {
            new() { Code = "USD", Symbol = "$", Name = "US Dollar" },
            new() { Code = "EUR", Symbol = "€", Name = "Euro" },
            new() { Code = "GBP", Symbol = "£", Name = "British Pound" },
            new() { Code = "JPY", Symbol = "¥", Name = "Japanese Yen" },
            new() { Code = "CAD", Symbol = "$", Name = "Canadian Dollar" },
            new() { Code = "AUD", Symbol = "$", Name = "Australian Dollar" },
            new() { Code = "CHF", Symbol = "CHF", Name = "Swiss Franc" },
            new() { Code = "CNY", Symbol = "¥", Name = "Chinese Yuan" },
            new() { Code = "INR", Symbol = "₹", Name = "Indian Rupee" },
            new() { Code = "MXN", Symbol = "$", Name = "Mexican Peso" },
            new() { Code = "BRL", Symbol = "R$", Name = "Brazilian Real" },
            new() { Code = "ARS", Symbol = "$", Name = "Argentine Peso" },
            new() { Code = "COP", Symbol = "$", Name = "Colombian Peso" },
            new() { Code = "CLP", Symbol = "$", Name = "Chilean Peso" },
            new() { Code = "PEN", Symbol = "S/.", Name = "Peruvian Sol" },
            new() { Code = "KRW", Symbol = "₩", Name = "South Korean Won" },
            new() { Code = "SEK", Symbol = "kr", Name = "Swedish Krona" },
            new() { Code = "NOK", Symbol = "kr", Name = "Norwegian Krone" },
            new() { Code = "DKK", Symbol = "kr", Name = "Danish Krone" },
            new() { Code = "PLN", Symbol = "zł", Name = "Polish Zloty" },
            new() { Code = "TRY", Symbol = "₺", Name = "Turkish Lira" },
            new() { Code = "ZAR", Symbol = "R", Name = "South African Rand" },
            new() { Code = "NZD", Symbol = "$", Name = "New Zealand Dollar" },
            new() { Code = "SGD", Symbol = "$", Name = "Singapore Dollar" },
            new() { Code = "HKD", Symbol = "$", Name = "Hong Kong Dollar" },
            new() { Code = "AED", Symbol = "AED", Name = "UAE Dirham" },
            new() { Code = "AFN", Symbol = "AFN", Name = "Afghan Afghani" },
            new() { Code = "ALL", Symbol = "ALL", Name = "Albanian Lek" },
            new() { Code = "AMD", Symbol = "AMD", Name = "Armenian Dram" },
            new() { Code = "ANG", Symbol = "ƒ", Name = "Netherlands Antillean Guilder" },
            new() { Code = "AOA", Symbol = "Kz", Name = "Angolan Kwanza" },
            new() { Code = "AWG", Symbol = "ƒ", Name = "Aruban Florin" },
            new() { Code = "AZN", Symbol = "AZN", Name = "Azerbaijani Manat" },
            new() { Code = "BAM", Symbol = "KM", Name = "Bosnia-Herzegovina Convertible Mark" },
            new() { Code = "BBD", Symbol = "$", Name = "Barbadian Dollar" },
            new() { Code = "BDT", Symbol = "Tk", Name = "Bangladeshi Taka" },
            new() { Code = "BGN", Symbol = "BGN", Name = "Bulgarian Lev" },
            new() { Code = "BHD", Symbol = "BD", Name = "Bahraini Dinar" },
            new() { Code = "BIF", Symbol = "FBu", Name = "Burundian Franc" },
            new() { Code = "BMD", Symbol = "$", Name = "Bermudian Dollar" },
            new() { Code = "BND", Symbol = "$", Name = "Brunei Dollar" },
            new() { Code = "BOB", Symbol = "Bs", Name = "Bolivian Boliviano" },
            new() { Code = "BSD", Symbol = "$", Name = "Bahamian Dollar" },
            new() { Code = "BTN", Symbol = "Nu.", Name = "Bhutanese Ngultrum" },
            new() { Code = "BWP", Symbol = "P", Name = "Botswana Pula" },
            new() { Code = "BYN", Symbol = "Br", Name = "Belarusian Ruble" },
            new() { Code = "BZD", Symbol = "BZ$", Name = "Belize Dollar" },
            new() { Code = "CDF", Symbol = "FC", Name = "Congolese Franc" },
            new() { Code = "CRC", Symbol = "₡", Name = "Costa Rican Colón" },
            new() { Code = "CUP", Symbol = "$", Name = "Cuban Peso" },
            new() { Code = "CVE", Symbol = "CVE", Name = "Cape Verdean Escudo" },
            new() { Code = "CZK", Symbol = "Kč", Name = "Czech Koruna" },
            new() { Code = "DJF", Symbol = "Fdj", Name = "Djiboutian Franc" },
            new() { Code = "DOP", Symbol = "RD$", Name = "Dominican Peso" },
            new() { Code = "DZD", Symbol = "DA", Name = "Algerian Dinar" },
            new() { Code = "EGP", Symbol = "E£", Name = "Egyptian Pound" },
            new() { Code = "ERN", Symbol = "Nfk", Name = "Eritrean Nakfa" },
            new() { Code = "ETB", Symbol = "Br", Name = "Ethiopian Birr" },
            new() { Code = "FJD", Symbol = "FJ$", Name = "Fijian Dollar" },
            new() { Code = "FKP", Symbol = "FKP", Name = "Falkland Islands Pound" },
            new() { Code = "GEL", Symbol = "GEL", Name = "Georgian Lari" },
            new() { Code = "GHS", Symbol = "GH₵", Name = "Ghanaian Cedi" },
            new() { Code = "GIP", Symbol = "GIP", Name = "Gibraltar Pound" },
            new() { Code = "GMD", Symbol = "D", Name = "Gambian Dalasi" },
            new() { Code = "GNF", Symbol = "FG", Name = "Guinean Franc" },
            new() { Code = "GTQ", Symbol = "Q", Name = "Guatemalan Quetzal" },
            new() { Code = "GYD", Symbol = "$", Name = "Guyanese Dollar" },
            new() { Code = "HNL", Symbol = "L", Name = "Honduran Lempira" },
            new() { Code = "HTG", Symbol = "G", Name = "Haitian Gourde" },
            new() { Code = "HUF", Symbol = "Ft", Name = "Hungarian Forint" },
            new() { Code = "IDR", Symbol = "Rp", Name = "Indonesian Rupiah" },
            new() { Code = "ILS", Symbol = "₪", Name = "Israeli New Shekel" },
            new() { Code = "IQD", Symbol = "IQD", Name = "Iraqi Dinar" },
            new() { Code = "IRR", Symbol = "IRR", Name = "Iranian Rial" },
            new() { Code = "ISK", Symbol = "kr", Name = "Icelandic Króna" },
            new() { Code = "JMD", Symbol = "J$", Name = "Jamaican Dollar" },
            new() { Code = "JOD", Symbol = "JD", Name = "Jordanian Dinar" },
            new() { Code = "KES", Symbol = "KSh", Name = "Kenyan Shilling" },
            new() { Code = "KGS", Symbol = "KGS", Name = "Kyrgyzstani Som" },
            new() { Code = "KHR", Symbol = "KHR", Name = "Cambodian Riel" },
            new() { Code = "KMF", Symbol = "CF", Name = "Comorian Franc" },
            new() { Code = "KPW", Symbol = "KPW", Name = "North Korean Won" },
            new() { Code = "KWD", Symbol = "KD", Name = "Kuwaiti Dinar" },
            new() { Code = "KYD", Symbol = "$", Name = "Cayman Islands Dollar" },
            new() { Code = "KZT", Symbol = "₸", Name = "Kazakhstani Tenge" },
            new() { Code = "LAK", Symbol = "LAK", Name = "Laotian Kip" },
            new() { Code = "LBP", Symbol = "L£", Name = "Lebanese Pound" },
            new() { Code = "LKR", Symbol = "Rs", Name = "Sri Lankan Rupee" },
            new() { Code = "LRD", Symbol = "$", Name = "Liberian Dollar" },
            new() { Code = "LSL", Symbol = "M", Name = "Lesotho Loti" },
            new() { Code = "LYD", Symbol = "LD", Name = "Libyan Dinar" },
            new() { Code = "MAD", Symbol = "DH", Name = "Moroccan Dirham" },
            new() { Code = "MDL", Symbol = "L", Name = "Moldovan Leu" },
            new() { Code = "MGA", Symbol = "Ar", Name = "Malagasy Ariary" },
            new() { Code = "MKD", Symbol = "MKD", Name = "Macedonian Denar" },
            new() { Code = "MMK", Symbol = "K", Name = "Myanmar Kyat" },
            new() { Code = "MNT", Symbol = "MNT", Name = "Mongolian Tugrik" },
            new() { Code = "MOP", Symbol = "MOP", Name = "Macanese Pataca" },
            new() { Code = "MRU", Symbol = "UM", Name = "Mauritanian Ouguiya" },
            new() { Code = "MUR", Symbol = "Rs", Name = "Mauritian Rupee" },
            new() { Code = "MVR", Symbol = "Rf", Name = "Maldivian Rufiyaa" },
            new() { Code = "MWK", Symbol = "MK", Name = "Malawian Kwacha" },
            new() { Code = "MYR", Symbol = "RM", Name = "Malaysian Ringgit" },
            new() { Code = "MZN", Symbol = "MT", Name = "Mozambican Metical" },
            new() { Code = "NAD", Symbol = "N$", Name = "Namibian Dollar" },
            new() { Code = "NGN", Symbol = "₦", Name = "Nigerian Naira" },
            new() { Code = "NIO", Symbol = "C$", Name = "Nicaraguan Córdoba" },
            new() { Code = "NPR", Symbol = "Rs", Name = "Nepalese Rupee" },
            new() { Code = "OMR", Symbol = "OMR", Name = "Omani Rial" },
            new() { Code = "PAB", Symbol = "B/.", Name = "Panamanian Balboa" },
            new() { Code = "PGK", Symbol = "K", Name = "Papua New Guinean Kina" },
            new() { Code = "PHP", Symbol = "₱", Name = "Philippine Peso" },
            new() { Code = "PKR", Symbol = "Rs", Name = "Pakistani Rupee" },
            new() { Code = "PYG", Symbol = "₲", Name = "Paraguayan Guarani" },
            new() { Code = "QAR", Symbol = "QAR", Name = "Qatari Riyal" },
            new() { Code = "RON", Symbol = "lei", Name = "Romanian Leu" },
            new() { Code = "RSD", Symbol = "RSD", Name = "Serbian Dinar" },
            new() { Code = "RUB", Symbol = "₽", Name = "Russian Ruble" },
            new() { Code = "RWF", Symbol = "RF", Name = "Rwandan Franc" },
            new() { Code = "SAR", Symbol = "SAR", Name = "Saudi Riyal" },
            new() { Code = "SBD", Symbol = "$", Name = "Solomon Islands Dollar" },
            new() { Code = "SCR", Symbol = "SCR", Name = "Seychellois Rupee" },
            new() { Code = "SDG", Symbol = "SDG", Name = "Sudanese Pound" },
            new() { Code = "SHP", Symbol = "SHP", Name = "Saint Helena Pound" },
            new() { Code = "SLE", Symbol = "Le", Name = "Sierra Leonean Leone" },
            new() { Code = "SOS", Symbol = "SOS", Name = "Somali Shilling" },
            new() { Code = "SRD", Symbol = "$", Name = "Surinamese Dollar" },
            new() { Code = "SSP", Symbol = "SSP", Name = "South Sudanese Pound" },
            new() { Code = "STN", Symbol = "Db", Name = "São Tomé and Príncipe Dobra" },
            new() { Code = "SYP", Symbol = "SYP", Name = "Syrian Pound" },
            new() { Code = "SZL", Symbol = "E", Name = "Swazi Lilangeni" },
            new() { Code = "THB", Symbol = "฿", Name = "Thai Baht" },
            new() { Code = "TJS", Symbol = "TJS", Name = "Tajikistani Somoni" },
            new() { Code = "TMT", Symbol = "TMT", Name = "Turkmenistani Manat" },
            new() { Code = "TND", Symbol = "DT", Name = "Tunisian Dinar" },
            new() { Code = "TOP", Symbol = "T$", Name = "Tongan Paʻanga" },
            new() { Code = "TTD", Symbol = "TT$", Name = "Trinidad and Tobago Dollar" },
            new() { Code = "TWD", Symbol = "NT$", Name = "New Taiwan Dollar" },
            new() { Code = "TZS", Symbol = "TSh", Name = "Tanzanian Shilling" },
            new() { Code = "UAH", Symbol = "₴", Name = "Ukrainian Hryvnia" },
            new() { Code = "UGX", Symbol = "USh", Name = "Ugandan Shilling" },
            new() { Code = "UYU", Symbol = "$U", Name = "Uruguayan Peso" },
            new() { Code = "UZS", Symbol = "UZS", Name = "Uzbekistani Som" },
            new() { Code = "VES", Symbol = "Bs.S", Name = "Venezuelan Bolívar" },
            new() { Code = "VND", Symbol = "₫", Name = "Vietnamese Dong" },
            new() { Code = "VUV", Symbol = "VT", Name = "Vanuatu Vatu" },
            new() { Code = "WST", Symbol = "WS$", Name = "Samoan Tala" },
            new() { Code = "XAF", Symbol = "FCFA", Name = "Central African CFA Franc" },
            new() { Code = "XCD", Symbol = "EC$", Name = "East Caribbean Dollar" },
            new() { Code = "XOF", Symbol = "CFA", Name = "West African CFA Franc" },
            new() { Code = "XPF", Symbol = "CFPF", Name = "CFP Franc" },
            new() { Code = "YER", Symbol = "YER", Name = "Yemeni Rial" },
            new() { Code = "ZMW", Symbol = "ZK", Name = "Zambian Kwacha" },
            new() { Code = "ZWG", Symbol = "ZWG", Name = "Zimbabwe Gold" },
        }.OrderBy(c => c.Code, StringComparer.Ordinal).ToList();

        private static readonly Dictionary<string, Currency> _currencyMap =
            _currencies.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        /// <summary>Symbols that several currencies use ("$", "¥", "kr"): alone they don't say which currency.</summary>
        private static readonly HashSet<string> _sharedSymbols = _currencies
            .GroupBy(c => c.Symbol).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();

        public const string BaseCurrencyPreferenceKey = "BaseCurrencyCode";

        /// <summary>The currencies shown without cents (Settings): comma-separated codes.</summary>
        public const string HiddenCentsPreferenceKey = "Currencies.HideCents";

        /// <summary>Currencies whose amounts are shown in whole units (loaded at startup, changed in Settings).</summary>
        private static HashSet<string> _hiddenCents = new(StringComparer.OrdinalIgnoreCase);

        private readonly IPreferencesService _preferences;

        public CurrencyService(IPreferencesService preferences)
        {
            _preferences = preferences;
        }

        /// <summary>
        /// Loads the saved base currency into <see cref="MoneyFormatter"/>, and the currencies shown without cents.
        /// Called once at startup, before the database is opened or any page formats money.
        /// </summary>
        public static void LoadSettings(IPreferencesService preferences)
        {
            MoneyFormatter.BaseCurrencyCode = preferences.Get(BaseCurrencyPreferenceKey, string.Empty);
            _hiddenCents = new HashSet<string>(
                preferences.Get(HiddenCentsPreferenceKey, string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        public string GetBaseCurrencyCode() => MoneyFormatter.BaseCurrency;

        public void SetBaseCurrencyCode(string code)
        {
            _preferences.Set(BaseCurrencyPreferenceKey, code);
            MoneyFormatter.BaseCurrencyCode = code;
        }

        public List<Currency> GetAvailableCurrencies() => _currencies;

        public Currency? GetCurrency(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            _currencyMap.TryGetValue(code, out var currency);
            return currency;
        }

        /// <summary>
        /// The currency's symbol; the base currency's for an empty code, the code itself for an unknown currency.
        /// </summary>
        public static string GetSymbol(string? code)
        {
            if (string.IsNullOrEmpty(code))
                code = MoneyFormatter.BaseCurrency;
            return _currencyMap.TryGetValue(code, out var currency) ? currency.Symbol : code;
        }

        /// <summary>True when the currency's symbol also belongs to another currency ("$" for COP and USD).</summary>
        public static bool HasSharedSymbol(string code) => _sharedSymbols.Contains(GetSymbol(code));

        /// <summary>
        /// Decimal places shown and accepted for a currency: 0 for JPY, CLP, KRW and the other whole-unit currencies, otherwise 2
        /// (the three-decimal dinars and rials stay at 2: storage is always in cents).
        /// Storage is always in cents (see <see cref="Money"/>).
        /// </summary>
        public static int GetMinorUnits(string? code) => code != null && _wholeUnitCodes.Contains(code) ? 0 : 2;

        /// <summary>The currencies without a minor unit (ISO 4217 exponent 0).</summary>
        private static readonly HashSet<string> _wholeUnitCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
        };

        /// <summary>
        /// Decimal places an amount in the currency is shown with: none when the user turned its cents off (Settings),
        /// else <see cref="GetMinorUnits"/>. Input still takes cents, so a typed or stored amount never loses them.
        /// </summary>
        public static int GetShownDecimals(string? code) =>
            code != null && _hiddenCents.Contains(code) ? 0 : GetMinorUnits(code);

        /// <summary>The currency has cents to show or hide (not JPY, CLP or KRW).</summary>
        public static bool HasCents(string code) => GetMinorUnits(code) > 0;

        public bool ShowsCents(string code) => GetShownDecimals(code) > 0;

        public void SetShowsCents(string code, bool show)
        {
            var hidden = new HashSet<string>(_hiddenCents, StringComparer.OrdinalIgnoreCase);
            if (show)
                hidden.Remove(code);
            else
                hidden.Add(code.ToUpperInvariant());
            _preferences.Set(HiddenCentsPreferenceKey, string.Join(",", hidden.Order(StringComparer.Ordinal)));
            _hiddenCents = hidden;
        }

        string ICurrencyService.GetSymbol(string code) => GetSymbol(code);

        bool ICurrencyService.HasCents(string code) => HasCents(code);

        /// <summary>The currency new accounts start with: the base currency.</summary>
        public string GetDefaultCurrencyCode() => GetBaseCurrencyCode();

        /// <summary>
        /// The currency of the device region (e.g. es-CO → COP), even when the app runs in another
        /// language (a fr-FR device shown in English still gets EUR).
        /// </summary>
        public static string GetDeviceCurrencyCode() => GetCurrencyCode(LocalizationService.DeviceCulture);

        /// <summary>
        /// The currency of a culture's region, or USD when the culture has no region
        /// or its currency is not supported.
        /// </summary>
        public static string GetCurrencyCode(CultureInfo culture)
        {
            if (culture.IsNeutralCulture || string.IsNullOrEmpty(culture.Name))
                return "USD";

            try
            {
                var isoCode = new RegionInfo(culture.Name).ISOCurrencySymbol;
                return _currencyMap.ContainsKey(isoCode) ? isoCode : "USD";
            }
            catch (ArgumentException)
            {
                return "USD";
            }
        }

        public string FormatAmount(decimal amount, string currencyCode)
        {
            return MoneyFormatter.Format(amount, currencyCode);
        }
    }
}
