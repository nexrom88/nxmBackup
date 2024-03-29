﻿using System.Web;
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

            bundles.Add(new ScriptBundle("~/bundles/jscookie").Include(
                        "~/Scripts/js-cookie/js.cookie.js"));

            bundles.Add(new ScriptBundle("~/bundles/jstree").Include(
                        "~/Scripts/jstree.js"));

            bundles.Add(new ScriptBundle("~/bundles/momentjs").Include(
            "~/Scripts/moment.min.js"));

            // Verwenden Sie die Entwicklungsversion von Modernizr zum Entwickeln und Erweitern Ihrer Kenntnisse. Wenn Sie dann
            // bereit ist für die Produktion, verwenden Sie das Buildtool unter https://modernizr.com, um nur die benötigten Tests auszuwählen.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/mustache").Include(
                        "~/Scripts/mustache.js"));

            bundles.Add(new ScriptBundle("~/bundles/popper").Include(
                        "~/Scripts/popper.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.bundle.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/bsInputSpinner").Include(
                        "~/Scripts/bsInputSpinner.js"));

            bundles.Add(new ScriptBundle("~/bundles/sweetalert2").Include(
                       "~/Scripts/sweetalert2.js"));

            bundles.Add(new Bundle("~/bundles/chart").Include(
                        "~/Scripts/chart.min.js"));

            //own Scripts
            bundles.Add(new ScriptBundle("~/bundles/nxmSettings").Include(
                 "~/Scripts/nxmSettings.js"));

            bundles.Add(new ScriptBundle("~/bundles/nxmRestore").Include(
                 "~/Scripts/nxmRestore.js"));

            bundles.Add(new ScriptBundle("~/bundles/nxmHelp").Include(
                 "~/Scripts/nxmHelp.js"));

            bundles.Add(new ScriptBundle("~/bundles/nxmMain").Include(
                      "~/Scripts/nxmMain.js"));

            bundles.Add(new ScriptBundle("~/bundles/restoreHeartbeatWebWorker").Include(
          "~/Scripts/restoreHeartbeatWebWorker.js"));

            bundles.Add(new ScriptBundle("~/bundles/globalAjaxHandler").Include(
                      "~/Scripts/globalAjaxHandler.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.min.css",
                      "~/Content/jstree/default/style.min.css",
                      "~/Content/font-awesome.css",
                      "~/Content/site.css"));
        }
    }
}
