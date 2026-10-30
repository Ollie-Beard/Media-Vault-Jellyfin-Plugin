using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaVault.Jellyfin.Configuration;

namespace MediaVault.Jellyfin;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer) => Instance = this;
    public static Plugin? Instance { get; private set; }
    public override string Name => "Media Vault Cloud Catalogue";
    public override Guid Id => Guid.Parse("f0413623-c78e-45e7-a114-352b0bfb205f");
    public IEnumerable<PluginPageInfo> GetPages() =>
    [new PluginPageInfo { Name = Name, EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace) }];
}


