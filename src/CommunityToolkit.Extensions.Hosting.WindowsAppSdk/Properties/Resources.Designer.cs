﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Uno.Extensions.Hosting.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Uno.Extensions.Hosting.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Build can only be called once..
        /// </summary>
        internal static string BuildCalled {
            get {
                return ResourceManager.GetString("BuildCalled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CreateBuilder must be called before CreateServiceProvider.
        /// </summary>
        internal static string CreateBuilderCallBeforeCreateServiceProvider {
            get {
                return ResourceManager.GetString("CreateBuilderCallBeforeCreateServiceProvider", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The IServiceProviderFactory returned a null IServiceProvider.
        /// </summary>
        internal static string NullIServiceProvider {
            get {
                return ResourceManager.GetString("NullIServiceProvider", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The resolver returned a null IServiceProviderFactory.
        /// </summary>
        internal static string ResolverReturnedNull {
            get {
                return ResourceManager.GetString("ResolverReturnedNull", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Replacing IHostApplicationLifetime is not supported..
        /// </summary>
        internal static string WindowsAppSdkHost_WindowsAppSdkHost_Replacing_IHostApplicationLifetime_is_not_supported_ {
            get {
                return ResourceManager.GetString("WindowsAppSdkHost_WindowsAppSdkHost_Replacing_IHostApplicationLifetime_is_not_sup" +
                        "ported_", resourceCulture);
            }
        }
    }
}
