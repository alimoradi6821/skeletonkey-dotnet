namespace SkeletonKey.Playwright.BrowserInstaller;

internal static class Program
{
    private static int Main(string[] args)
    {
        string browser = args.Length == 0 ? "chromium" : args[0];
        if (!IsSupportedBrowser(browser))
        {
            Console.Error.WriteLine("Usage: skeletonkey.playwright-installer [chromium|firefox|webkit|all]");
            return 2;
        }

        string[] playwrightArgs = string.Equals(browser, "all", StringComparison.Ordinal)
            ? ["install"]
            : ["install", browser];

        return Microsoft.Playwright.Program.Main(playwrightArgs);
    }

    private static bool IsSupportedBrowser(string browser)
    {
        return browser is "chromium" or "firefox" or "webkit" or "all";
    }
}
