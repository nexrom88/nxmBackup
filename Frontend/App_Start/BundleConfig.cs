using System.Web;
using System.Web.Optimization;

namespace Frontend
{
    public class BundleConfig
    {
        // Weitere Informationen zur Bündelung finden Sie unter https://go.microsoft.com/fwlink/?LinkId=301862.
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jquery.cookie").Include(
                        "~/Scripts/jquery.cookie.js"));

            bundles.Add(new ScriptBundle("~/bundles/jstree").Include(
                        "~/Scripts/jsTree3/jsTree.js"));

            // Verwenden Sie die Entwicklungsversion von Modernizr zum Entwickeln und Erweitern Ihrer Kenntnisse. Wenn Sie dann
            // bereit ist für die Produktion, verwenden Sie das Buildtool unter https://modernizr.com, um nur die benötigten Tests auszuwählen.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/mustache").Include(
                        "~/Scripts/mustache.js"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js"));

            bundles.Add(new ScriptBundle("~/bundles/bsInputSpinner").Include(
                        "~/Scripts/bsInputSpinner.js"));

            bundles.Add(new ScriptBundle("~/bundles/sweetalert2").Include(
                       "~/Scripts/sweetalert2.js"));

            //own Scripts
            bundles.Add(new ScriptBundle("~/bundles/nxmMain").Include(
                      "~/Scripts/nxmMain.js"));

            bundles.Add(new ScriptBundle("~/bundles/globalAjaxHandler").Include(
                      "~/Scripts/globalAjaxHandler.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/jsTree/themes/default/style.min.css",
                      "~/Content/font-awesome.css",
                      "~/Content/site.css"));
        }
    }
}
