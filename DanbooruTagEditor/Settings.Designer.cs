﻿//------------------------------------------------------------------------------
// <auto-generated>
//     このコードはツールによって生成されました。
//     ランタイム バージョン:4.0.30319.42000
//
//     このファイルへの変更は、以下の状況下で不正な動作の原因になったり、
//     コードが再生成されるときに損失したりします。
// </auto-generated>
//------------------------------------------------------------------------------

namespace DanbooruTagEditor {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.12.0.0")]
    public sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("S:\\VisualStudioProjects\\DanbooruTagEditor\\DanbooruTagEditor\\csv\\translateMap.csv")]
        public string translateCsvPath {
            get {
                return ((string)(this["translateCsvPath"]));
            }
            set {
                this["translateCsvPath"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("S:\\VisualStudioProjects\\DanbooruTagEditor\\DanbooruTagEditor\\json\\categoryColorMap" +
            ".json")]
        public string categoryColorMapJsonPath {
            get {
                return ((string)(this["categoryColorMapJsonPath"]));
            }
            set {
                this["categoryColorMapJsonPath"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("S:\\VisualStudioProjects\\DanbooruTagEditor\\DanbooruTagEditor\\json\\tagCategoryMap.j" +
            "son")]
        public string tagCategoryMapJsonPath {
            get {
                return ((string)(this["tagCategoryMapJsonPath"]));
            }
            set {
                this["tagCategoryMapJsonPath"] = value;
            }
        }
    }
}