using System.Collections.Generic;
using UnityEngine;

namespace GF
{
    public enum ILLanguage
    {
        None = -1,
        English = 0, //英语
        Chinese, //中文
        ChineseTW, //中文-台湾
        Japanese, //日语
        Korean, //韩语
        French, //法语
        German, //德语
        Russian, //俄语
        Italian, //意大利语
        Portuguese, //葡萄牙语
        Spanish, //西班牙语
        Dutch, //荷兰语
        Vietnamese, //越南语
        Turkish, //土耳其语
        Indonesian, //印尼语
        Polish, //波兰语
        Malay, //马来语
        Thai, //泰语
        Greek, //希腊语
        Arabic, //阿拉伯语
        Hebrew, //希伯来语
        Swedish, //瑞典语
        Danish, //丹麦语
        Finnish, //芬兰
        Icelandic, //冰岛
        Afrikaans, //南非语
        Albanian, //阿尔巴尼亚语
        Amharic, //阿姆哈拉语
        Armenian, //亚美尼亚语
        Azerbaijani, //阿塞拜疆语
        Bangla, //孟加拉语
        Basque, //巴斯克语
        Belarusian, //白俄罗斯语
        Bulgarian, //保加利亚语
        Burmese, //缅甸语
        Catalan, //加泰罗尼亚语
        ChineseHK, //中文-香港
        Croatian, //克罗地亚语
        Czech, //捷克语
        Estonian, //爱沙尼亚语
        FrenchCA, //法语-加拿大
        Galician, //加利西亚语
        Georgian, //格鲁吉亚语
        Gujarati, //古吉拉特语
        Hindi, //印地语
        Hungarian, //匈牙利语
        Kannada, //卡纳达语
        Kazakh, //哈萨克语
        Khmer, //高棉语
        Kyrgyz, //吉尔吉斯语
        Lao, //老挝语
        Latvian, //拉脱维亚语
        Lithuanian, //立陶宛语
        Macedonian, //马其顿语
        MalayMalaysia, //马来语（马来西亚）
        Malayalam, //马拉雅拉姆语
        Marathi, //马拉地语
        Mongolian, //蒙古语
        Nepali, //尼泊尔语
        Norwegian, //挪威语
        Persian, //波斯语
        PortugueseBR, //葡萄牙语-巴西
        Punjabi, //旁遮普语
        Romanian, //罗马尼亚语
        Serbian, //塞尔维亚语
        Sinhala, //僧伽罗语
        Slovak, //斯洛伐克语
        Slovenian, //斯洛文尼亚语
        SpanishUS, //西班牙语-美国
        Swahili, //斯瓦希里语
        Tagalog, //他加禄语
        Tamil, //泰米尔语
        Telugu, //泰卢固语
        Ukrainian, //乌克兰语
        Urdu, //乌尔都语
        Zulu, //祖鲁语
    }

    public class I2Kit
    {
        private readonly Dictionary<ILLanguage, string> LanguageCodeMap = new Dictionary<ILLanguage, string>()
        {
            {ILLanguage.English, "en"}, {ILLanguage.Chinese, "zh-CN"}, {ILLanguage.ChineseTW, "zh-TW"},
            {ILLanguage.Japanese, "ja"}, {ILLanguage.Korean, "ko"}, {ILLanguage.French, "fr-FR"},
            {ILLanguage.German, "de-DE"}, {ILLanguage.Russian, "ru"}, {ILLanguage.Italian, "it-IT"},
            {ILLanguage.Portuguese, "pt-PT"}, {ILLanguage.Spanish, "es-ES"}, {ILLanguage.Dutch, "nl-NL"},
            {ILLanguage.Vietnamese, "vi"}, {ILLanguage.Turkish, "tr"}, {ILLanguage.Indonesian, "id"},
            {ILLanguage.Polish, "pl"}, {ILLanguage.Malay, "ms"}, {ILLanguage.Thai, "th"},
            {ILLanguage.Greek, "el"}, {ILLanguage.Arabic, "ar"}, {ILLanguage.Hebrew, "he"},
            {ILLanguage.Swedish, "sv-SE"}, {ILLanguage.Danish, "da"}, {ILLanguage.Finnish, "fi"},
            {ILLanguage.Icelandic, "is"}, {ILLanguage.Afrikaans, "af"}, {ILLanguage.Albanian, "sq"},
            {ILLanguage.Amharic, "am"}, {ILLanguage.Armenian, "hy"}, {ILLanguage.Azerbaijani, "az"},
            {ILLanguage.Bangla, "bn"}, {ILLanguage.Basque, "eu-ES"}, {ILLanguage.Belarusian, "be"},
            {ILLanguage.Bulgarian, "bg"}, {ILLanguage.Burmese, "my"}, {ILLanguage.Catalan, "ca"},
            {ILLanguage.ChineseHK, "zh-HK"}, {ILLanguage.Croatian, "hr"}, {ILLanguage.Czech, "cs"},
            {ILLanguage.Estonian, "et"}, {ILLanguage.FrenchCA, "fr-CA"}, {ILLanguage.Galician, "gl-ES"},
            {ILLanguage.Georgian, "ka"}, {ILLanguage.Gujarati, "gu"}, {ILLanguage.Hindi, "hi"},
            {ILLanguage.Hungarian, "hu"}, {ILLanguage.Kannada, "kn"}, {ILLanguage.Kazakh, "kk"},
            {ILLanguage.Khmer, "km"}, {ILLanguage.Kyrgyz, "ky"}, {ILLanguage.Lao, "lo"},
            {ILLanguage.Latvian, "lv"}, {ILLanguage.Lithuanian, "lt"}, {ILLanguage.Macedonian, "mk"},
            {ILLanguage.MalayMalaysia, "ms-MY"}, {ILLanguage.Malayalam, "ml"}, {ILLanguage.Marathi, "mr"},
            {ILLanguage.Mongolian, "mn"}, {ILLanguage.Nepali, "ne"}, {ILLanguage.Norwegian, "nb"},
            {ILLanguage.Persian, "fa"}, {ILLanguage.PortugueseBR, "pt-BR"}, {ILLanguage.Punjabi, "pa"},
            {ILLanguage.Romanian, "ro"}, {ILLanguage.Serbian, "sr"}, {ILLanguage.Sinhala, "si"},
            {ILLanguage.Slovak, "sk"}, {ILLanguage.Slovenian, "sl"}, {ILLanguage.SpanishUS, "es-US"},
            {ILLanguage.Swahili, "sw"}, {ILLanguage.Tagalog, "tl"}, {ILLanguage.Tamil, "ta"},
            {ILLanguage.Telugu, "te"}, {ILLanguage.Ukrainian, "uk"}, {ILLanguage.Urdu, "ur"},
            {ILLanguage.Zulu, "zu"},
        };
    
        //为none表示语言还未设置
        public ILLanguage CurrentLanguage = ILLanguage.None;

        public void InitGameLanguage()
        {
            ChangeLanguage(GetLanguageCodeBySystemLanguage());
        }

        public string Translate(string termKey)
        {
            return I2.Loc.LocalizationManager.GetTranslation(termKey);
        }

        public void ChangeLanguage(ILLanguage language)
        {
            // // --------todo:临时修改，后续需要修改---------
            // if (language == ILLanguage.Hindi)
            // {
            //     language = ILLanguage.English;
            // }
            // // ----------------------------------------
            //
            CurrentLanguage = language;
            if (LanguageCodeMap.TryGetValue(language, out string str))
            {
                I2.Loc.LocalizationManager.CurrentLanguageCode = str;
            }
            else
            {
                I2.Loc.LocalizationManager.CurrentLanguageCode = LanguageCodeMap[ILLanguage.English];
            }
            // EventMg.Instance.Raise(new EventChangeLanguage());
        }
        
        public bool ChangeLanguage(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return false;
            }
        
            ILLanguage key = ILLanguage.English;
            bool hasKey = false;
            foreach (var kv in LanguageCodeMap)
            {
                if (kv.Value == language)
                {
                    key = kv.Key;
                    hasKey = true;
                    break;
                }
            }
        
            if (hasKey)
            {
                ChangeLanguage(key);
                App.Event.DispatchEvent(Define.Event.CHANGE_LANGUAGE_INTERNAL);
                return true;
            }

            return false;
        }

        public bool CurrentIsRTLLanguage() => I2.Loc.LocalizationManager.IsRight2Left;

        public string GetLanguageStr(ILLanguage language)
        {
            if (ILLanguage.None == language)
            {
                language = GetLanguageCodeBySystemLanguage();
            }

            if (LanguageCodeMap.TryGetValue(language, out string languageStr))
            {
                return languageStr;
            }
            else
            {
                return "";
            }
        }

        private ILLanguage GetLanguageCodeBySystemLanguage()
        {
            ILLanguage language = ILLanguage.English;
            if (Application.isEditor)
            {
                return language;
            }

            switch (Application.systemLanguage)
            {
                case SystemLanguage.Afrikaans:
                    language = ILLanguage.Afrikaans;
                    break;
                case SystemLanguage.Arabic:
                    language = ILLanguage.Arabic;
                    break;
                case SystemLanguage.Basque:
                    language = ILLanguage.Basque;
                    break;
                case SystemLanguage.Belarusian:
                    language = ILLanguage.Belarusian;
                    break;
                case SystemLanguage.Bulgarian:
                    language = ILLanguage.Bulgarian;
                    break;
                case SystemLanguage.Catalan:
                    language = ILLanguage.Catalan;
                    break;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                    language = ILLanguage.Chinese;
                    break;
                case SystemLanguage.ChineseTraditional:
                    language = ILLanguage.ChineseTW;
                    break;
                case SystemLanguage.Czech:
                    language = ILLanguage.Czech;
                    break;
                case SystemLanguage.Danish:
                    language = ILLanguage.Danish;
                    break;
                case SystemLanguage.Dutch:
                    language = ILLanguage.Dutch;
                    break;
                case SystemLanguage.English:
                    language = ILLanguage.English;
                    break;
                case SystemLanguage.Estonian:
                    language = ILLanguage.Estonian;
                    break;
                case SystemLanguage.Finnish:
                    language = ILLanguage.Finnish;
                    break;
                case SystemLanguage.French:
                    language = ILLanguage.French;
                    break;
                case SystemLanguage.German:
                    language = ILLanguage.German;
                    break;
                case SystemLanguage.Greek:
                    language = ILLanguage.Greek;
                    break;
                case SystemLanguage.Hebrew:
                    language = ILLanguage.Hebrew;
                    break;
                case SystemLanguage.Hungarian:
                    language = ILLanguage.Hungarian;
                    break;
                case SystemLanguage.Icelandic:
                    language = ILLanguage.Icelandic;
                    break;
                case SystemLanguage.Indonesian:
                    language = ILLanguage.Indonesian;
                    break;
                case SystemLanguage.Italian:
                    language = ILLanguage.Italian;
                    break;
                case SystemLanguage.Japanese:
                    language = ILLanguage.Japanese;
                    break;
                case SystemLanguage.Korean:
                    language = ILLanguage.Korean;
                    break;
                case SystemLanguage.Latvian:
                    language = ILLanguage.Latvian;
                    break;
                case SystemLanguage.Lithuanian:
                    language = ILLanguage.Lithuanian;
                    break;
                case SystemLanguage.Norwegian:
                    language = ILLanguage.Norwegian;
                    break;
                case SystemLanguage.Polish:
                    language = ILLanguage.Polish;
                    break;
                case SystemLanguage.Portuguese:
                    language = ILLanguage.Portuguese;
                    break;
                case SystemLanguage.Romanian:
                    language = ILLanguage.Romanian;
                    break;
                case SystemLanguage.Russian:
                    language = ILLanguage.Russian;
                    break;
                case SystemLanguage.SerboCroatian:
                    language = ILLanguage.Serbian;
                    break;
                case SystemLanguage.Slovak:
                    language = ILLanguage.Slovak;
                    break;
                case SystemLanguage.Slovenian:
                    language = ILLanguage.Slovenian;
                    break;
                case SystemLanguage.Spanish:
                    language = ILLanguage.Spanish;
                    break;
                case SystemLanguage.Swedish:
                    language = ILLanguage.Swedish;
                    break;
                case SystemLanguage.Thai:
                    language = ILLanguage.Thai;
                    break;
                case SystemLanguage.Turkish:
                    language = ILLanguage.Turkish;
                    break;
                case SystemLanguage.Ukrainian:
                    language = ILLanguage.Ukrainian;
                    break;
                case SystemLanguage.Vietnamese:
                    language = ILLanguage.Vietnamese;
                    break;
                case SystemLanguage.Unknown:
                    language = GetUnknownLanguage();
                    break;
                default:
                    language = ILLanguage.English;
                    break;
            }

            return language;
        }
        
        private void SetUnknownLanguage()
        {
            //Debug.LogError("SetUnknownLanguage");
            // string lang = IPMConfig.IPM_LANGUAGE;
            ILLanguage defaultLanguage = ILLanguage.English;
            // foreach (var kv in LanguageCodeMap)
            // {
            //     if (kv.Value != lang) continue;
            //     defaultLanguage = kv.Key;
            //     break;
            // }

            ChangeLanguage(defaultLanguage);
        }

        private ILLanguage GetUnknownLanguage()
        {
            // string lang = IPMConfig.IPM_LANGUAGE;
            ILLanguage defaultLanguage = ILLanguage.English;
            // foreach (var kv in LanguageCodeMap)
            // {
            //     if (kv.Value != lang) continue;
            //     defaultLanguage = kv.Key;
            //     break;
            // }

            return defaultLanguage;
        }
    }
}