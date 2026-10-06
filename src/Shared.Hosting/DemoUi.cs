using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

namespace Shared.Hosting;

public static class DemoUi
{
    public static void UseDemoUi(this WebApplication app, string demo)
    {
        var page = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "web", demo));
        var assets = new PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "web", "common"));
        var files = new CompositeFileProvider(page, assets);
        app.Lifetime.ApplicationStopped.Register(() => { page.Dispose(); assets.Dispose(); });
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    }
}
