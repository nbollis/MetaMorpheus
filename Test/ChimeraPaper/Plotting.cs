using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Test.ChimeraPaper.ResultFiles;
using Plotly.NET;
using Plotly.NET.ImageExport;
using Chart = Plotly.NET.CSharp.Chart;
using System.IO;
using Microsoft.FSharp.Core;
using Plotly.NET.LayoutObjects;
using Plotly.NET.TraceObjects;
using System.Windows.Media.Media3D;
using EngineLayer.FdrAnalysis;


namespace Test.ChimeraPaper
{
    public static class Plotting
    {
        #region Filters

        public static string[] AcceptableConditionsToPlotIndividualFileComparisonBottomUp =
        {
            "MetaMorpheusWithLibrary", "MetaMorpheusNoChimerasWithLibrary",
            "ReviewdDatabaseNoPhospho_MsFraggerDDA", "ReviewdDatabaseNoPhospho_MsFraggerDDA+", "ReviewdDatabaseNoPhospho_MsFragger",
        };

        public static string[] AcceptableConditionsToPlotIndividualFileComparisonTopDown =
        {
            "MetaMorpheus", "MetaMorpheusNoChimeras", 
            "MsPathFinderT", "MsPathFinderTWithMods", "MsPathFinderTWithModsNoChimeras", "MsPathFinderTWithMods_7",
            "ProsightPD"
        };

        public static string[] AcceptableConditionsToPlotInternalMMComparisonBottomUp =
        {
            "MetaMorpheusWithLibrary", "MetaMorpheusNoChimerasWithLibrary",
        };

        public static string[] AcceptableConditonsToPlotInternalMMComparisonTopDown =
        {
            "MetaMorpheus", "MetaMorpheusNoChimeras"
        };

        public static string[] AcceptableConditionsToPlotBulkResultComparisonBottomUp =
        {
            "MetaMorpheusWithLibrary", "MetaMorpheusNoChimerasWithLibrary", 
            "ReviewdDatabaseNoPhospho_MsFraggerDDA+", "ReviewdDatabaseNoPhospho_MsFragger",
        };

        public static string[] AcceptableConditionsToPlotBulkResultsComparisonTopDown =
        {
            "MetaMorpheus", "MetaMorpheusNoChimeras", 
            "MsPathFinderT", "MsPathFinderTWithMods", "MsPathFinderTWithModsNoChimeras", "MsPathFinderTWithMods_7",
            "ProsightPD"
        };

        public static string[] AcceptableConditionsToPlotFDRComparisonResults =
        {
            // Bottom Up
            "MetaMorpheusWithLibrary"
        };

        public static string[] AcceptableConditionsToPlotChimeraBreakdownTopDown =
        {
            "MetaMorpheus"
        };


        /// <summary>
        /// Colors for plots
        /// MetaMorpheus -> Purple
        /// Fragger -> Blue
        /// Chimerys ->
        /// MsPathFinderT -> Yellow
        /// ProsightPD -> Red
        /// </summary>
        public static Dictionary<string, Color> ConditionToColorDictionary = new()
        {
            // Bottom Up
            {"MetaMorpheusWithLibrary", Color.fromKeyword(ColorKeyword.Purple) }, 
            {"MetaMorpheusNoChimerasWithLibrary", Color.fromKeyword(ColorKeyword.Plum) },
            //{"MsFragger", Color.fromKeyword(ColorKeyword.LightAkyBlue) }, // Old fragger params
            //{"MsFraggerDDA+", Color.fromKeyword(ColorKeyword.RoyalBlue) },
            {"ReviewdDatabaseNoPhospho_MsFraggerDDA", Color.fromKeyword(ColorKeyword.LightAkyBlue) },
            {"ReviewdDatabaseNoPhospho_MsFragger", Color.fromKeyword(ColorKeyword.LightAkyBlue) },
            {"ReviewdDatabaseNoPhospho_MsFraggerDDA+", Color.fromKeyword(ColorKeyword.RoyalBlue) },

            // General
            {"Chimeras", Color.fromKeyword(ColorKeyword.Purple) },
            {"No Chimeras", Color.fromKeyword(ColorKeyword.Plum) },

            // Top Down
            {"MetaMorpheus", Color.fromKeyword(ColorKeyword.Purple) },
            {"MetaMorpheusNoChimeras", Color.fromKeyword(ColorKeyword.Plum) },
            {"MsPathFinderTWithModsNoChimeras", Color.fromKeyword(ColorKeyword.Moccasin)},
            {"MsPathFinderT", Color.fromKeyword(ColorKeyword.Khaki)},
            {"MsPathFinderTWithMods", Color.fromKeyword(ColorKeyword.Gold)},
            {"MsPathFinderTWithMods_7", Color.fromKeyword(ColorKeyword.GoldenRod)},
            {"ProsightPD", Color.fromKeyword(ColorKeyword.Red)},


            // Chimera Breakdown plot
            {"Isolated Species", Color.fromKeyword(ColorKeyword.LightAkyBlue)},
            {"Unique Proteoform", Color.fromKeyword(ColorKeyword.MediumVioletRed)},
            {"Unique Peptidoform", Color.fromKeyword(ColorKeyword.MediumVioletRed)},
            {"Unique Protein", Color.fromKeyword(ColorKeyword.MediumAquamarine)},
            {"Targets", Color.fromKeyword(ColorKeyword.LightAkyBlue)},
            {"Decoys", Color.fromKeyword(ColorKeyword.Gold)},
        };

        public static Dictionary<string, string> FileNameConversionDictionary = new()
        {
            // Bottom Up Mann-11
            { "20100604_Velos1_TaGe_SA_A549_1", "A549_1_1" },
            { "20100604_Velos1_TaGe_SA_A549_2", "A549_1_2" },
            { "20100604_Velos1_TaGe_SA_A549_3", "A549_1_3" },
            { "20100604_Velos1_TaGe_SA_A549_4", "A549_1_4" },
            { "20100604_Velos1_TaGe_SA_A549_5", "A549_1_5" },
            { "20100604_Velos1_TaGe_SA_A549_6", "A549_1_6" },
            { "20100721_Velos1_TaGe_SA_A549_01", "A549_2_1" },
            { "20100721_Velos1_TaGe_SA_A549_02", "A549_2_2" },
            { "20100721_Velos1_TaGe_SA_A549_03", "A549_2_3" },
            { "20100721_Velos1_TaGe_SA_A549_04", "A549_2_4" },
            { "20100721_Velos1_TaGe_SA_A549_05", "A549_2_5" },
            { "20100721_Velos1_TaGe_SA_A549_06", "A549_2_6" },
            { "20101215_Velos1_TaGe_SA_A549_01", "A549_3_1" },
            { "20101215_Velos1_TaGe_SA_A549_02", "A549_3_2" },
            { "20101215_Velos1_TaGe_SA_A549_03", "A549_3_3" },
            { "20101215_Velos1_TaGe_SA_A549_04", "A549_3_4" },
            { "20101215_Velos1_TaGe_SA_A549_05", "A549_3_5" },
            { "20101215_Velos1_TaGe_SA_A549_06", "A549_3_6" },
            { "20100609_Velos1_TaGe_SA_GAMG_1", "GAMG_1_1" },
            { "20100609_Velos1_TaGe_SA_GAMG_2", "GAMG_1_2" },
            { "20100609_Velos1_TaGe_SA_GAMG_3", "GAMG_1_3" },
            { "20100609_Velos1_TaGe_SA_GAMG_4", "GAMG_1_4" },
            { "20100609_Velos1_TaGe_SA_GAMG_5", "GAMG_1_5" },
            { "20100609_Velos1_TaGe_SA_GAMG_6", "GAMG_1_6" },
            { "20100723_Velos1_TaGe_SA_Gamg_1", "GAMG_2_1" },
            { "20100723_Velos1_TaGe_SA_Gamg_2", "GAMG_2_2" },
            { "20100723_Velos1_TaGe_SA_Gamg_3", "GAMG_2_3" },
            { "20100723_Velos1_TaGe_SA_Gamg_4", "GAMG_2_4" },
            { "20100723_Velos1_TaGe_SA_Gamg_5", "GAMG_2_5" },
            { "20100723_Velos1_TaGe_SA_Gamg_6", "GAMG_2_6" },
            { "20101227_Velos1_TaGe_SA_GAMG1", "GAMG_3_1" },
            { "20101227_Velos1_TaGe_SA_GAMG_101230100451", "GAMG_3_1" },
            { "20101227_Velos1_TaGe_SA_GAMG2_101229143203", "GAMG_3_2" },
            { "20101227_Velos1_TaGe_SA_GAMG2", "GAMG_3_2" },
            { "20101227_Velos1_TaGe_SA_GAMG3", "GAMG_3_3" },
            { "20101227_Velos1_TaGe_SA_GAMG4", "GAMG_3_4" },
            { "20101227_Velos1_TaGe_SA_GAMG5", "GAMG_3_5" },
            { "20101227_Velos1_TaGe_SA_GAMG6", "GAMG_3_6" },
            { "20100609_Velos1_TaGe_SA_Hek293_01", "HEK293_1_1" },
            { "20100609_Velos1_TaGe_SA_Hek293_02", "HEK293_1_2" },
            { "20100609_Velos1_TaGe_SA_Hek293_03", "HEK293_1_3" },
            { "20100609_Velos1_TaGe_SA_Hek293_04", "HEK293_1_4" },
            { "20100609_Velos1_TaGe_SA_Hek293_05", "HEK293_1_5" },
            { "20100609_Velos1_TaGe_SA_Hek293_06", "HEK293_1_6" },
            { "20100609_Velos1_TaGe_SA_293_1", "HEK293_1_1" },
            { "20100609_Velos1_TaGe_SA_293_2", "HEK293_1_2" },
            { "20100609_Velos1_TaGe_SA_293_3", "HEK293_1_3" },
            { "20100609_Velos1_TaGe_SA_293_4", "HEK293_1_4" },
            { "20100609_Velos1_TaGe_SA_293_5", "HEK293_1_5" },
            { "20100609_Velos1_TaGe_SA_293_6", "HEK293_1_6" },
            { "20101227_Velos1_TaGe_SA_HEK293_01", "HEK293_2_1" },
            { "20101227_Velos1_TaGe_SA_HEK293_02", "HEK293_2_2" },
            { "20101227_Velos1_TaGe_SA_HEK293_03", "HEK293_2_3" },
            { "20101227_Velos1_TaGe_SA_HEK293_04", "HEK293_2_4" },
            { "20101227_Velos1_TaGe_SA_HEK293_05", "HEK293_2_5" },
            { "20101227_Velos1_TaGe_SA_HEK293_06", "HEK293_2_6" },
            { "20100723_Velos1_TaGe_SA_Hek293_01", "HEK293_3_1" },
            { "20100723_Velos1_TaGe_SA_Hek293_02", "HEK293_3_2" },
            { "20100723_Velos1_TaGe_SA_Hek293_03", "HEK293_3_3" },
            { "20100723_Velos1_TaGe_SA_Hek293_04", "HEK293_3_4" },
            { "20100723_Velos1_TaGe_SA_Hek293_05", "HEK293_3_5" },
            { "20100723_Velos1_TaGe_SA_Hek293_06", "HEK293_3_6" },
            { "20100611_Velos1_TaGe_SA_Hela_1", "Hela_1_1" },
            { "20100611_Velos1_TaGe_SA_Hela_2", "Hela_1_2" },
            { "20100611_Velos1_TaGe_SA_Hela_3", "Hela_1_3" },
            { "20100611_Velos1_TaGe_SA_Hela_4", "Hela_1_4" },
            { "20100611_Velos1_TaGe_SA_Hela_5", "Hela_1_5" },
            { "20100611_Velos1_TaGe_SA_Hela_6", "Hela_1_6" },
            { "20100726_Velos1_TaGe_SA_HeLa_3", "Hela_2_1" },
            { "20100726_Velos1_TaGe_SA_HeLa_4", "Hela_2_2" },
            { "20100726_Velos1_TaGe_SA_HeLa_5", "Hela_2_3" },
            { "20100726_Velos1_TaGe_SA_HeLa_6", "Hela_2_4" },
            { "20100726_Velos1_TaGe_SA_HeLa_1", "Hela_2_5" },
            { "20100726_Velos1_TaGe_SA_HeLa_2", "Hela_2_6" },
            { "20101224_Velos1_TaGe_SA_HeLa_05", "Hela_3_1" },
            { "20101224_Velos1_TaGe_SA_HeLa_06", "Hela_3_2" },
            { "20101224_Velos1_TaGe_SA_HeLa_01", "Hela_3_3" },
            { "20101224_Velos1_TaGe_SA_HeLa_02", "Hela_3_4" },
            { "20101224_Velos1_TaGe_SA_HeLa_03", "Hela_3_5" },
            { "20101224_Velos1_TaGe_SA_HeLa_04", "Hela_3_6" },
            { "20100726_Velos1_TaGe_SA_HepG2_1", "HepG2_2_1" },
            { "20100726_Velos1_TaGe_SA_HepG2_2", "HepG2_2_2" },
            { "20100726_Velos1_TaGe_SA_HepG2_3", "HepG2_2_3" },
            { "20100726_Velos1_TaGe_SA_HepG2_4", "HepG2_2_4" },
            { "20100726_Velos1_TaGe_SA_HepG2_5", "HepG2_2_5" },
            { "20100726_Velos1_TaGe_SA_HepG2_6", "HepG2_2_6" },
            { "20101224_Velos1_TaGe_SA_HepG2_1", "HepG2_3_1" },
            { "20101224_Velos1_TaGe_SA_HepG2_2", "HepG2_3_2" },
            { "20101224_Velos1_TaGe_SA_HepG2_3", "HepG2_3_3" },
            { "20101224_Velos1_TaGe_SA_HepG2_4", "HepG2_3_4" },
            { "20101224_Velos1_TaGe_SA_HepG2_5", "HepG2_3_5" },
            { "20101224_Velos1_TaGe_SA_HepG2_6", "HepG2_3_6" },
            { "20100611_Velos1_TaGe_SA_HepG2_1", "HepG2_1_1" },
            { "20100611_Velos1_TaGe_SA_HepG2_2", "HepG2_1_2" },
            { "20100611_Velos1_TaGe_SA_HepG2_3", "HepG2_1_3" },
            { "20100611_Velos1_TaGe_SA_HepG2_4", "HepG2_1_4" },
            { "20100611_Velos1_TaGe_SA_HepG2_5", "HepG2_1_5" },
            { "20100611_Velos1_TaGe_SA_HepG2_6", "HepG2_1_6" },
            { "20100614_Velos1_TaGe_SA_Jurkat_1", "Jurkat_1_1" },
            { "20100614_Velos1_TaGe_SA_Jurkat_2", "Jurkat_1_2" },
            { "20100614_Velos1_TaGe_SA_Jurkat_3", "Jurkat_1_3" },
            { "20100614_Velos1_TaGe_SA_Jurkat_4", "Jurkat_1_4" },
            { "20100614_Velos1_TaGe_SA_Jurkat_5", "Jurkat_1_5" },
            { "20100614_Velos1_TaGe_SA_Jurkat_6", "Jurkat_1_6" },
            { "20100730_Velos1_TaGe_SA_Jurkat_01", "Jurkat_2_1" },
            { "20100730_Velos1_TaGe_SA_Jurkat_02", "Jurkat_2_2" },
            { "20100730_Velos1_TaGe_SA_Jurkat_03", "Jurkat_2_3" },
            { "20100730_Velos1_TaGe_SA_Jurkat_04", "Jurkat_2_4" },
            { "20100730_Velos1_TaGe_SA_Jurkat_05", "Jurkat_2_5" },
            { "20100730_Velos1_TaGe_SA_Jurkat_06_100731121305", "Jurkat_2_6" },
            { "20101230_Velos1_TaGe_SA_Jurkat1", "Jurkat_3_1" },
            { "20101230_Velos1_TaGe_SA_Jurkat2", "Jurkat_3_2" },
            { "20101230_Velos1_TaGe_SA_Jurkat3", "Jurkat_3_3" },
            { "20101230_Velos1_TaGe_SA_Jurkat4", "Jurkat_3_4" },
            { "20101230_Velos1_TaGe_SA_Jurkat5", "Jurkat_3_5" },
            { "20101230_Velos1_TaGe_SA_Jurkat6", "Jurkat_3_6" },
            { "20100614_Velos1_TaGe_SA_K562_1", "K562_1_1" },
            { "20100614_Velos1_TaGe_SA_K562_2", "K562_1_2" },
            { "20100614_Velos1_TaGe_SA_K562_3", "K562_1_3" },
            { "20100614_Velos1_TaGe_SA_K562_4", "K562_1_4" },
            { "20100614_Velos1_TaGe_SA_K562_5", "K562_1_5" },
            { "20100614_Velos1_TaGe_SA_K562_6", "K562_1_6" },
            { "20100730_Velos1_TaGe_SA_K562_1", "K562_2_1" },
            { "20100730_Velos1_TaGe_SA_K562_2", "K562_2_2" },
            { "20100730_Velos1_TaGe_SA_K564_3", "K562_2_3" },
            { "20100730_Velos1_TaGe_SA_K565_4", "K562_2_4" },
            { "20100730_Velos1_TaGe_SA_K565_5", "K562_2_5" },
            { "20100730_Velos1_TaGe_SA_K565_6", "K562_2_6" },
            { "20101222_Velos1_TaGe_SA_K562_01", "K562_3_1" },
            { "20101222_Velos1_TaGe_SA_K562_02", "K562_3_2" },
            { "20101222_Velos1_TaGe_SA_K562_03", "K562_3_3" },
            { "20101222_Velos1_TaGe_SA_K562_04", "K562_3_4" },
            { "20101222_Velos1_TaGe_SA_K562_05", "K562_3_5" },
            { "20101222_Velos1_TaGe_SA_K562_06", "K562_3_6" },
            { "20100618_Velos1_TaGe_SA_LanCap_1", "LanCap_1_1" },
            { "20100618_Velos1_TaGe_SA_LanCap_2", "LanCap_1_2" },
            { "20100618_Velos1_TaGe_SA_LanCap_3", "LanCap_1_3" },
            { "20100618_Velos1_TaGe_SA_LanCap_4", "LanCap_1_4" },
            { "20100618_Velos1_TaGe_SA_LanCap_5", "LanCap_1_5" },
            { "20100618_Velos1_TaGe_SA_LanCap_6", "LanCap_1_6" },
            { "20100719_Velos1_TaGe_SA_LnCap_1", "LanCap_2_1" },
            { "20100719_Velos1_TaGe_SA_LnCap_2", "LanCap_2_2" },
            { "20100719_Velos1_TaGe_SA_LnCap_3", "LanCap_2_3" },
            { "20100719_Velos1_TaGe_SA_LnCap_4", "LanCap_2_4" },
            { "20100719_Velos1_TaGe_SA_LnCap_5", "LanCap_2_5" },
            { "20100719_Velos1_TaGe_SA_LnCap_6", "LanCap_2_6" },
            { "20101210_Velos1_AnWe_SA_LnCap_1", "LanCap_3_1" },
            { "20101210_Velos1_AnWe_SA_LnCap_2", "LanCap_3_2" },
            { "20101210_Velos1_AnWe_SA_LnCap_3", "LanCap_3_3" },
            { "20101210_Velos1_AnWe_SA_LnCap_4", "LanCap_3_4" },
            { "20101210_Velos1_AnWe_SA_LnCap_5", "LanCap_3_5" },
            { "20101210_Velos1_AnWe_SA_LnCap_6", "LanCap_3_6" },
            { "20100616_Velos1_TaGe_SA_MCF7_1", "MCF7_1_1" },
            { "20100616_Velos1_TaGe_SA_MCF7_2", "MCF7_1_2" },
            { "20100616_Velos1_TaGe_SA_MCF7_3", "MCF7_1_3" },
            { "20100616_Velos1_TaGe_SA_MCF7_4", "MCF7_1_4" },
            { "20100616_Velos1_TaGe_SA_MCF7_5", "MCF7_1_5" },
            { "20100616_Velos1_TaGe_SA_MCF7_6", "MCF7_1_6" },
            { "20100719_Velos1_TaGe_SA_MCF7_01", "MCF7_2_1" },
            { "20100719_Velos1_TaGe_SA_MCF7_02", "MCF7_2_2" },
            { "20100719_Velos1_TaGe_SA_MCF7_03", "MCF7_2_3" },
            { "20100719_Velos1_TaGe_SA_MCF7_04", "MCF7_2_4" },
            { "20100719_Velos1_TaGe_SA_MCF7_05", "MCF7_2_5" },
            { "20100719_Velos1_TaGe_SA_MCF7_06", "MCF7_2_6" },
            { "20101210_Velos1_AnWe_SA_MCF7_1", "MCF7_3_1" },
            { "20101210_Velos1_AnWe_SA_MCF7_2", "MCF7_3_2" },
            { "20101210_Velos1_AnWe_SA_MCF7_3", "MCF7_3_3" },
            { "20101210_Velos1_AnWe_SA_MCF7_4", "MCF7_3_4" },
            { "20101210_Velos1_AnWe_SA_MCF7_5", "MCF7_3_5" },
            { "20101210_Velos1_AnWe_SA_MCF7_6", "MCF7_3_6" },
            { "20100616_Velos1_TaGe_SA_RKO_1", "RKO_1_1" },
            { "20100616_Velos1_TaGe_SA_RKO_2", "RKO_1_2" },
            { "20100616_Velos1_TaGe_SA_RKO_3", "RKO_1_3" },
            { "20100616_Velos1_TaGe_SA_RKO_4", "RKO_1_4" },
            { "20100616_Velos1_TaGe_SA_RKO_5", "RKO_1_5" },
            { "20100616_Velos1_TaGe_SA_RKO_6", "RKO_1_6" },
            { "20100801_Velos1_TaGe_SA_RKO_01", "RKO_2_1" },
            { "20100801_Velos1_TaGe_SA_RKO_02", "RKO_2_2" },
            { "20100801_Velos1_TaGe_SA_RKO_03", "RKO_2_3" },
            { "20100801_Velos1_TaGe_SA_RKO_04", "RKO_2_4" },
            { "20100805_Velos1_TaGe_SA_RKO_05", "RKO_2_5" },
            { "20100805_Velos1_TaGe_SA_RKO_06", "RKO_2_6" },
            { "20101223_Velos1_TaGe_SA_RKO_01", "RKO_3_1" },
            { "20101223_Velos1_TaGe_SA_RKO_02", "RKO_3_2" },
            { "20101223_Velos1_TaGe_SA_RKO_03", "RKO_3_3" },
            { "20101223_Velos1_TaGe_SA_RKO_04", "RKO_3_4" },
            { "20101223_Velos1_TaGe_SA_RKO_05", "RKO_3_5" },
            { "20101223_Velos1_TaGe_SA_RKO_06", "RKO_3_6" },
            { "20100618_Velos1_TaGe_SA_U2OS_1", "U20S_1_1" },
            { "20100618_Velos1_TaGe_SA_U2OS_2", "U20S_1_2" },
            { "20100618_Velos1_TaGe_SA_U2OS_3", "U20S_1_3" },
            { "20100618_Velos1_TaGe_SA_U2OS_4", "U20S_1_4" },
            { "20100618_Velos1_TaGe_SA_U2OS_5", "U20S_1_5" },
            { "20100618_Velos1_TaGe_SA_U2OS_6", "U20S_1_6" },
            { "20100721_Velos1_TaGe_SA_U2OS_1", "U20S_2_1" },
            { "20100721_Velos1_TaGe_SA_U2OS_2", "U20S_2_2" },
            { "20100721_Velos1_TaGe_SA_U2OS_3", "U20S_2_3" },
            { "20100721_Velos1_TaGe_SA_U2OS_4", "U20S_2_4" },
            { "20100721_Velos1_TaGe_SA_U2OS_5", "U20S_2_5" },
            { "20100721_Velos1_TaGe_SA_U2OS_6", "U20S_2_6" },
            { "20101210_Velos1_AnWe_SA_U2OS_1", "U20S_3_1" },
            { "20101210_Velos1_AnWe_SA_U2OS_2", "U20S_3_2" },
            { "20101210_Velos1_AnWe_SA_U2OS_3", "U20S_3_3" },
            { "20101210_Velos1_AnWe_SA_U2OS_4", "U20S_3_4" },
            { "20101210_Velos1_AnWe_SA_U2OS_5", "U20S_3_5" },
            { "20101210_Velos1_AnWe_SA_U2OS_6", "U20S_3_6" },

            // Top Down Jurkat
            {"02-17-20_jurkat_td_rep1_fract1","1_01"},
            {"02-17-20_jurkat_td_rep1_fract2","1_02"},
            {"02-17-20_jurkat_td_rep1_fract3","1_03"},
            {"02-17-20_jurkat_td_rep1_fract4","1_04"},
            {"02-17-20_jurkat_td_rep2_fract1","2_01"},
            {"02-17-20_jurkat_td_rep2_fract2","2_02"},
            {"02-17-20_jurkat_td_rep2_fract3","2_03"},
            {"02-17-20_jurkat_td_rep2_fract4","2_04"},
            {"02-18-20_jurkat_td_rep1_fract10","1_10"},
            {"02-18-20_jurkat_td_rep1_fract5","1_05"},
            {"02-18-20_jurkat_td_rep1_fract6","1_06"},
            {"02-18-20_jurkat_td_rep1_fract7","1_07"},
            {"02-18-20_jurkat_td_rep1_fract8","1_08"},
            {"02-18-20_jurkat_td_rep1_fract9","1_09"},
            {"02-18-20_jurkat_td_rep2_fract10","2_10"},
            {"02-18-20_jurkat_td_rep2_fract5","2_05"},
            {"02-18-20_jurkat_td_rep2_fract6","2_06"},
            {"02-18-20_jurkat_td_rep2_fract7","2_07"},
            {"02-18-20_jurkat_td_rep2_fract8","2_08"},
            {"02-18-20_jurkat_td_rep2_fract9","2_09"},
            {"Ecoli_SEC1_F1","1_01"},
            {"Ecoli_SEC1_F2","1_02"},
            {"Ecoli_SEC1_F3","1_03"},
            {"Ecoli_SEC1_F4","1_04"},
            {"Ecoli_SEC3_F1","3_01"},
            {"Ecoli_SEC3_F10","3_10"},
            {"Ecoli_SEC3_F11","3_11"},
            {"Ecoli_SEC3_F12","3_12"},
            {"Ecoli_SEC3_F13","3_13"},
            {"Ecoli_SEC3_F2","3_02"},
            {"Ecoli_SEC3_F3","3_03"},
            {"Ecoli_SEC3_F4","3_04"},
            {"Ecoli_SEC3_F5","3_05"},
            {"Ecoli_SEC3_F6","3_06"},
            {"Ecoli_SEC3_F7","3_07"},
            {"Ecoli_SEC3_F8","3_08"},
            {"Ecoli_SEC3_F9","3_09"},
            {"Ecoli_SEC4_F1","4_01"},
            {"Ecoli_SEC4_F10","4_10"},
            {"Ecoli_SEC4_F11","4_11"},
            {"Ecoli_SEC4_F12","4_12"},
            {"Ecoli_SEC4_F2","4_02"},
            {"Ecoli_SEC4_F3","4_03"},
            {"Ecoli_SEC4_F4","4_04"},
            {"Ecoli_SEC4_F5","4_05"},
            {"Ecoli_SEC4_F6","4_06"},
            {"Ecoli_SEC4_F7","4_07"},
            {"Ecoli_SEC4_F8","4_08"},
            {"Ecoli_SEC4_F9","4_09"},
            {"Ecoli_SEC5_F1","5_01"},
            {"Ecoli_SEC5_F10","5_10"},
            {"Ecoli_SEC5_F11","5_11"},
            {"Ecoli_SEC5_F12","5_12"},
            {"Ecoli_SEC5_F13","5_13"},
            {"Ecoli_SEC5_F14","5_14"},
            {"Ecoli_SEC5_F2","5_02"},
            {"Ecoli_SEC5_F3","5_03"},
            {"Ecoli_SEC5_F4","5_04"},
            {"Ecoli_SEC5_F5","5_05"},
            {"Ecoli_SEC5_F6","5_06"},
            {"Ecoli_SEC5_F7","5_07"},
            {"Ecoli_SEC5_F8","5_08"},
            {"Ecoli_SEC5_F9","5_09"},
        };

        public static IEnumerable<string> ConvertFileNames(this IEnumerable<string> fileNames)
        {
            return fileNames.Select(p => p.Replace("-calib", "").Replace("-averaged", ""))
                .Select(p => FileNameConversionDictionary.ContainsKey(p) ? FileNameConversionDictionary[p] : p);
        }

        public static string ConvertFileName(this string fileName)
        {
            var name = fileName.Replace("-calib", "").Replace("-averaged", "");
            return FileNameConversionDictionary.ContainsKey(name) ? FileNameConversionDictionary[name] : fileName;
        }

        public static Dictionary<string, string> ConitionNameConversionDictionary = new()
        {
            // Bottom up
            { "MetaMorpheusWithLibrary", "MetaMorpheus⠀" },
            { "MetaMorpheusNoChimerasWithLibrary", "MetaMorpheus No Chimeras" },
            { "ReviewdDatabaseNoPhospho_MsFragger", "MsFragger" },
            { "ReviewdDatabaseNoPhospho_MsFraggerDDA", "MsFragger" },
            { "ReviewdDatabaseNoPhospho_MsFraggerDDA+", "MsFraggerDDA+" },

            // Top Down
            { "MetaMorpheus", "MetaMorpheus\u2800" },
            { "MetaMorpheusNoChimeras", "MetaMorpheus No Chimeras" },
        };

        public static IEnumerable<string> ConvertConditionNames(this IEnumerable<string> conditions)
        {
            return conditions.Select(p => ConitionNameConversionDictionary.ContainsKey(p) ? ConitionNameConversionDictionary[p] : p);
        }

        public static string ConvertConditionName(this string condition)
        {
            return ConitionNameConversionDictionary.ContainsKey(condition) ? ConitionNameConversionDictionary[condition] : condition;
        }

        #endregion

        #region Plotly Things

        public static int DefaultHeight = 600;
        public static Layout DefaultLayout => Layout.init<string>(PaperBGColor: Color.fromKeyword(ColorKeyword.White), PlotBGColor: Color.fromKeyword(ColorKeyword.White));

        private static Layout DefaultLayoutWithLegend => Layout.init<string>(
            PaperBGColor: Color.fromKeyword(ColorKeyword.White),
            PlotBGColor: Color.fromKeyword(ColorKeyword.White),
            ShowLegend: true,
            Legend: Legend.init(X: 0.5, Y: -0.2, Orientation: StyleParam.Orientation.Horizontal, EntryWidth: 0,
                VerticalAlign: StyleParam.VerticalAlign.Bottom,
                XAnchor: StyleParam.XAnchorPosition.Center,
                YAnchor: StyleParam.YAnchorPosition.Top
            ));

        #endregion

        #region Cell Line

        public static void PlotIndividualFileResults(this CellLineResults cellLine)
        {
            string outPath = Path.Combine(cellLine.GetFigureDirectory(), $"{FileIdentifiers.IndividualFileComparisonFigure}_{cellLine.CellLine}");
            cellLine.GetIndividualFileResults(out int width, out int height).SavePNG(outPath, null, width, height);
        }

        private static GenericChart.GenericChart GetIndividualFileResults(this CellLineResults cellLine, out int width, out int height)
        {
            var selector = cellLine.First().IsTopDown
                ? AcceptableConditionsToPlotIndividualFileComparisonTopDown
                : AcceptableConditionsToPlotIndividualFileComparisonBottomUp;
            var individualFileResults = cellLine.Results.Select(p => p.IndividualFileComparisonFile)
                .Where(p => selector.Contains(p.First().Condition))
                .OrderBy(p => p.First().Condition.ConvertConditionName())
                .ToList();
            var labels = individualFileResults.SelectMany(p => p.Results.Select(m => m.FileName))
                .ConvertFileNames().Distinct().ToList();

            GenericChart.GenericChart chart;
            string resultType;
            if (cellLine.First().IsTopDown)
            {
                individualFileResults.ForEach(p => p.Results = p.Results.OrderBy(m => m.FileName.ConvertFileName()).ToList());
                labels = labels.OrderBy(p => p.ConvertFileName()).ToList();

                chart = Chart.Combine(individualFileResults.Select(p =>
                    Chart2D.Chart.Column<int, string, string, int, int>(p.Select(m => m.OnePercentPsmCount), labels, null,
                        p.Results.First().Condition.ConvertConditionName(), MarkerColor: ConditionToColorDictionary[p.First().Condition])));
                resultType = "PrSMs";
            }
            else
            {
                chart = Chart.Combine(individualFileResults.Select(p =>
                    Chart2D.Chart.Column<int, string, string, int, int>(p.Select(m => m.OnePercentPeptideCount), labels, null,
                        p.Results.First().Condition.ConvertConditionName(), MarkerColor: ConditionToColorDictionary[p.First().Condition])));
                resultType = "Peptides";
            }
            
            width = 50 * labels.Count + 10 * individualFileResults.Count;
            height = DefaultHeight;
            chart.WithTitle($"{cellLine.CellLine} 1% FDR {resultType}")
                .WithXAxisStyle(Title.init("File"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend)
                .WithSize(width, height);
            return chart;
        }

        public static void PlotCellLineChimeraBreakdown(this CellLineResults cellLine)
        {
            var selector = cellLine.First().IsTopDown
                ? AcceptableConditionsToPlotChimeraBreakdownTopDown
                : AcceptableConditionsToPlotFDRComparisonResults;
            var smLabel = cellLine.First().IsTopDown ? "PrSM" : "PSM";
            var pepLabel = cellLine.First().IsTopDown ? "Proteoform" : "Peptide";

            var results = cellLine.Results
                .Where(p => p is MetaMorpheusResult && selector.Contains(p.Condition))
                .SelectMany(p => ((MetaMorpheusResult)p).ChimeraBreakdownFile)
                .ToList();
            var psmChart =
                results.GetChimeraBreakDownStackedColumn(ChimeraBreakdownType.Psm, cellLine.First().IsTopDown, out int width);
            string psmOutPath = Path.Combine(cellLine.GetFigureDirectory(),
                $"{FileIdentifiers.ChimeraBreakdownComparisonFigure}_{smLabel}_{cellLine.CellLine}");
            psmChart.SavePNG(psmOutPath, null, width, DefaultHeight);

            var peptideChart =
                results.GetChimeraBreakDownStackedColumn(ChimeraBreakdownType.Peptide, cellLine.First().IsTopDown, out width);
            string peptideOutPath = Path.Combine(cellLine.GetFigureDirectory(),
                $"{FileIdentifiers.ChimeraBreakdownComparisonFigure}_{pepLabel}_{cellLine.CellLine}");
            peptideChart.SavePNG(peptideOutPath, null, width, DefaultHeight);
        }

        public static void PlotCellLineChimeraBreakdown_TargetDecoy(this CellLineResults cellLine)
        {
            var selector = cellLine.First().IsTopDown
                ? AcceptableConditionsToPlotChimeraBreakdownTopDown
                : AcceptableConditionsToPlotFDRComparisonResults;
            var smLabel = cellLine.First().IsTopDown ? "PrSM" : "PSM";
            var pepLabel = cellLine.First().IsTopDown ? "Proteoform" : "Peptide";

            var results = cellLine.Results
                .Where(p => p is MetaMorpheusResult && selector.Contains(p.Condition))
                .SelectMany(p => ((MetaMorpheusResult)p).ChimeraBreakdownFile)
                .ToList();
            var psmChart =
                results.GetChimeraBreakDownStackedColumn_TargetDecoy(ChimeraBreakdownType.Psm, cellLine.First().IsTopDown, out int width);
            string psmOutPath = Path.Combine(cellLine.GetFigureDirectory(),
                $"{FileIdentifiers.ChimeraBreakdownTargetDecoy}_{smLabel}_{cellLine.CellLine}");
            psmChart.SavePNG(psmOutPath, null, width, DefaultHeight);

            var peptideChart =
                results.GetChimeraBreakDownStackedColumn_TargetDecoy(ChimeraBreakdownType.Peptide, cellLine.First().IsTopDown, out width);
            string peptideOutPath = Path.Combine(cellLine.GetFigureDirectory(),
                $"{FileIdentifiers.ChimeraBreakdownTargetDecoy}_{pepLabel}_{cellLine.CellLine}");
            peptideChart.SavePNG(peptideOutPath, null, width, DefaultHeight);
        }


        public static void PlotCellLineRetentionTimePredictions(this CellLineResults cellLine)
        {
            var plots = cellLine.GetCellLineRetentionTimePredictions();
            string outPath = Path.Combine(cellLine.GetFigureDirectory(), $"{FileIdentifiers.ChronologerFigure}_{cellLine.CellLine}");
            plots.Chronologer.SavePNG(outPath, null, 1000, DefaultHeight);

            outPath = Path.Combine(cellLine.GetFigureDirectory(), $"{FileIdentifiers.SSRCalcFigure}_{cellLine.CellLine}");
            plots.SSRCalc3.SavePNG(outPath, null, 1000, DefaultHeight);
        }

        private static (GenericChart.GenericChart Chronologer, GenericChart.GenericChart SSRCalc3) GetCellLineRetentionTimePredictions(this CellLineResults cellLine)
        {
            var individualFiles = cellLine.Results
                .Where(p => AcceptableConditionsToPlotFDRComparisonResults.Contains(p.Condition))
                .OrderBy(p => ((MetaMorpheusResult)p).RetentionTimePredictionFile.First())
                .Select(p => ((MetaMorpheusResult)p).RetentionTimePredictionFile)
                .ToList();
            var chronologer = individualFiles
                .SelectMany(p => p.Where(m => m.ChronologerPrediction != 0 && m.PeptideModSeq != ""))
                .ToList();
            var ssrCalc = individualFiles
                .SelectMany(p => p.Where(m => m.SSRCalcPrediction is not 0 or double.NaN or -1))
                .ToList();

            var chronologerPlot = Chart.Combine(new[]
                {
                    Chart2D.Chart.Scatter<double, double, string>(
                        chronologer.Where(p => !p.IsChimeric).Select(p => p.RetentionTime),
                        chronologer.Where(p => !p.IsChimeric).Select(p => p.ChronologerPrediction), StyleParam.Mode.Markers,
                        "No Chimeras", MarkerColor: ConditionToColorDictionary["No Chimeras"]),
                    Chart2D.Chart.Scatter<double, double, string>(
                        chronologer.Where(p => p.IsChimeric).Select(p => p.RetentionTime),
                        chronologer.Where(p => p.IsChimeric).Select(p => p.ChronologerPrediction), StyleParam.Mode.Markers,
                        "Chimeras", MarkerColor: ConditionToColorDictionary["Chimeras"])
                })
                .WithTitle($"{cellLine.CellLine} Chronologer Predicted HI vs Retention Time (1% Peptides)")
                .WithXAxisStyle(Title.init("Retention Time"))
                .WithYAxisStyle(Title.init("Chronologer Prediction"))
                .WithLayout(DefaultLayoutWithLegend)
                .WithSize(1000, DefaultHeight);

            var ssrCalcPlot = Chart.Combine(new[]
                {
                    Chart2D.Chart.Scatter<double, double, string>(
                        ssrCalc.Where(p => !p.IsChimeric).Select(p => p.RetentionTime),
                        ssrCalc.Where(p => !p.IsChimeric).Select(p => p.SSRCalcPrediction), StyleParam.Mode.Markers,
                        "No Chimeras", MarkerColor: ConditionToColorDictionary["No Chimeras"]),
                    Chart2D.Chart.Scatter<double, double, string>(
                        ssrCalc.Where(p => p.IsChimeric).Select(p => p.RetentionTime),
                        ssrCalc.Where(p => p.IsChimeric).Select(p => p.SSRCalcPrediction), StyleParam.Mode.Markers,
                        "Chimeras", MarkerColor: ConditionToColorDictionary["Chimeras"])
                })
                .WithTitle($"{cellLine.CellLine} SSRCalc3 Predicted HI vs Retention Time (1% Peptides)")
                .WithXAxisStyle(Title.init("Retention Time"))
                .WithYAxisStyle(Title.init("SSRCalc3 Prediction"))
                .WithLayout(DefaultLayoutWithLegend)
                .WithSize(1000, DefaultHeight);
            return (chronologerPlot, ssrCalcPlot);
        }


        public static void PlotCellLineSpectralSimilarity(this CellLineResults cellLine)
        {
            string outpath = Path.Combine(cellLine.GetFigureDirectory(), $"{FileIdentifiers.SpectralAngleFigure}_{cellLine.CellLine}");
            cellLine.GetCellLineSpectralSimilarity().SavePNG(outpath);
        }
        private static GenericChart.GenericChart GetCellLineSpectralSimilarity(this CellLineResults cellLine)
        {
            var individualFiles = cellLine.Results
                .Where(p => AcceptableConditionsToPlotFDRComparisonResults.Contains(p.Condition))
                .OrderBy(p => ((MetaMorpheusResult)p).RetentionTimePredictionFile.First())
                .Select(p => ((MetaMorpheusResult)p).RetentionTimePredictionFile)
                .ToList();
            var results = individualFiles.SelectMany(p => p.Where(m => m.SpectralAngle is not -1 or double.NaN))
                .ToList();
            
            var violin = Chart.Combine(new[]
                {
                    Chart.Violin<string, double, string> (
                        new Plotly.NET.CSharp.Optional<IEnumerable<string>>(Enumerable.Repeat("Chimeras", results.Count(p => p.IsChimeric)), true),
                        new Plotly.NET.CSharp.Optional<IEnumerable<double>>(results.Where(p => p.IsChimeric).Select(p => p.SpectralAngle), true),
                        null, MarkerColor:  ConditionToColorDictionary["Chimeras"], MeanLine: MeanLine.init(true,  ConditionToColorDictionary["Chimeras"]),
                        ShowLegend: false), 
                    Chart.Violin<string, double, string> (
                        new Plotly.NET.CSharp.Optional<IEnumerable<string>>(Enumerable.Repeat("No Chimeras", results.Count(p => !p.IsChimeric)), true),
                        new Plotly.NET.CSharp.Optional<IEnumerable<double>>(results.Where(p => !p.IsChimeric).Select(p => p.SpectralAngle), true),
                        null, MarkerColor:  ConditionToColorDictionary["No Chimeras"], MeanLine: MeanLine.init(true,  ConditionToColorDictionary["No Chimeras"]),
                        ShowLegend: false)

                })
                .WithTitle($"{cellLine.CellLine} Spectral Angle Distribution (1% Peptides)")
                .WithYAxisStyle(Title.init("Spectral Angle"))
                .WithLayout(DefaultLayout)
                .WithSize(1000, 600);
            return violin;
        }

        #endregion

        #region Bulk Result

        public static void PlotInternalMMComparison(this AllResults allResults)
        {
            var selector = allResults.First().First().IsTopDown
                ? AcceptableConditonsToPlotInternalMMComparisonTopDown
                : AcceptableConditionsToPlotInternalMMComparisonBottomUp;
            var results = allResults.CellLineResults.SelectMany(p => p.BulkResultCountComparisonFile.Results)
                .Where(p => selector.Contains(p.Condition))
                .ToList();
            var labels = results.Select(p => p.DatasetName).Distinct().ConvertConditionNames().ToList();

            var noChimeras = results.Where(p => p.Condition.Contains("NoChimeras")).ToList();
            var withChimeras = results.Where(p => !p.Condition.Contains("NoChimeras")).ToList();

            var psmChart = Chart.Combine(new[]
            {
                Chart2D.Chart.Column<int, string, string, int, int>(noChimeras.Select(p => p.OnePercentPsmCount),
                    labels, null, "No Chimeras", MarkerColor: ConditionToColorDictionary[noChimeras.First().Condition]),
                Chart2D.Chart.Column<int, string, string, int, int>(withChimeras.Select(p => p.OnePercentPsmCount),
                    labels, null, "Chimeras", MarkerColor: ConditionToColorDictionary[withChimeras.First().Condition])
            });
            var smLabel = allResults.First().First().IsTopDown ? "PrSMs" : "PSMs";
            psmChart.WithTitle($"MetaMorpheus 1% FDR {smLabel}")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);
            string psmOutpath = Path.Combine(allResults.GetFigureDirectory(), $"InternalMetaMorpheusComparison_{smLabel}");
            psmChart.SavePNG(psmOutpath);

            var peptideChart = Chart.Combine(new[]
            {
                Chart2D.Chart.Column<int, string, string, int, int>(noChimeras.Select(p => p.OnePercentPeptideCount),
                    labels, null, "No Chimeras", MarkerColor: ConditionToColorDictionary[noChimeras.First().Condition]),
                Chart2D.Chart.Column<int, string, string, int, int>(withChimeras.Select(p => p.OnePercentPeptideCount),
                    labels, null, "Chimeras", MarkerColor: ConditionToColorDictionary[withChimeras.First().Condition])
            });
            peptideChart.WithTitle($"MetaMorpheus 1% FDR {allResults.First().First().ResultType}s")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);
            string peptideOutpath = Path.Combine(allResults.GetFigureDirectory(), $"InternalMetaMorpheusComparison_{allResults.First().First().ResultType}s");
            peptideChart.SavePNG(peptideOutpath);

            var proteinChart = Chart.Combine(new[]
            {
                Chart2D.Chart.Column<int, string, string, int, int>(noChimeras.Select(p => p.OnePercentProteinGroupCount),
                    labels, null, "No Chimeras", MarkerColor: ConditionToColorDictionary[noChimeras.First().Condition]),
                Chart2D.Chart.Column<int, string, string, int, int>(withChimeras.Select(p => p.OnePercentProteinGroupCount),
                    labels, null, "Chimeras", MarkerColor: ConditionToColorDictionary[withChimeras.First().Condition])
            });
            proteinChart.WithTitle("MetaMorpheus 1% FDR Proteins")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);
            string proteinOutpath = Path.Combine(allResults.GetFigureDirectory(), "InternalMetaMorpheusComparison_Proteins");
            proteinChart.SavePNG(proteinOutpath);
        }

        public static void PlotBulkResultComparison(this AllResults allResults)
        {
            var selector = allResults.First().First().IsTopDown
                ? AcceptableConditionsToPlotBulkResultsComparisonTopDown
                : AcceptableConditionsToPlotBulkResultComparisonBottomUp;
            var results = allResults.CellLineResults.SelectMany(p => p.BulkResultCountComparisonFile.Results)
                .Where(p => selector.Contains(p.Condition))
                .OrderBy(p => p.Condition.ConvertConditionName())
                .ToList();
            var labels = results.Select(p => p.DatasetName).Distinct().ConvertConditionNames().ToList();

            var psmCharts = new List<GenericChart.GenericChart>();
            var peptideCharts = new List<GenericChart.GenericChart>();
            var proteinCharts = new List<GenericChart.GenericChart>();
            foreach (var condition in results.Select(p => p.Condition).Distinct())
            {
                var conditionSpecificResults = results.Where(p => p.Condition == condition).ToList();

                var conditionToWrite = condition.ConvertConditionName();
                psmCharts.Add(Chart2D.Chart.Column<int, string, string, int, int>(
                    conditionSpecificResults.Select(p => p.OnePercentPsmCount), labels, null, conditionToWrite,
                    MarkerColor: ConditionToColorDictionary[condition]));
                peptideCharts.Add(Chart2D.Chart.Column<int, string, string, int, int>(
                    conditionSpecificResults.Select(p => p.OnePercentPeptideCount), labels, null, conditionToWrite,
                    MarkerColor: ConditionToColorDictionary[condition]));
                proteinCharts.Add(Chart2D.Chart.Column<int, string, string, int, int>(
                    conditionSpecificResults.Select(p => p.OnePercentProteinGroupCount), labels, null, conditionToWrite,
                    MarkerColor: ConditionToColorDictionary[condition]));
            }
            var smLabel = allResults.First().First().IsTopDown ? "PrSMs" : "PSMs";
            var psmChart = Chart.Combine(psmCharts).WithTitle($"1% FDR {smLabel}")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);
            var peptideChart = Chart.Combine(peptideCharts).WithTitle($"1% FDR {allResults.First().First().ResultType}s")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);
            var proteinChart = Chart.Combine(proteinCharts).WithTitle("1% FDR Proteins")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);

            var psmPath = Path.Combine(allResults.GetFigureDirectory(), $"BulkResultComparison_{smLabel}");
            var peptidePath = Path.Combine(allResults.GetFigureDirectory(), $"BulkResultComparison_{allResults.First().First().ResultType}s");
            var proteinpath = Path.Combine(allResults.GetFigureDirectory(), "BulkResultComparison_Proteins");
            psmChart.SavePNG(psmPath);
            peptideChart.SavePNG(peptidePath);
            proteinChart.SavePNG(proteinpath);
        }

        public static void PlotBulkResultChimeraBreakDown(this AllResults allResults)
        {
            var selector = allResults.First().First().IsTopDown
                ? AcceptableConditionsToPlotChimeraBreakdownTopDown
                : AcceptableConditionsToPlotFDRComparisonResults;
            bool isTopDown = allResults.First().First().IsTopDown;
            var smLabel = isTopDown ? "PrSM" : "PSM";
            var pepLabel = isTopDown ? "Proteoform" : "Peptide";
            var results = allResults.SelectMany(z => z.Results
                .Where(p => p is MetaMorpheusResult && selector.Contains(p.Condition))
                .SelectMany(p => ((MetaMorpheusResult)p).ChimeraBreakdownFile.Results))
                .ToList();
            var psmChart =
                results.GetChimeraBreakDownStackedColumn(ChimeraBreakdownType.Psm, isTopDown, out int width);
            var psmOutPath = Path.Combine(allResults.GetFigureDirectory(),
                               $"AllResults_{FileIdentifiers.ChimeraBreakdownComparisonFigure}{smLabel}s");
            psmChart.SavePNG(psmOutPath, null, width, DefaultHeight);

            var peptideChart =
                results.GetChimeraBreakDownStackedColumn(ChimeraBreakdownType.Peptide, isTopDown, out width);
            var peptideOutPath = Path.Combine(allResults.GetFigureDirectory(),
                               $"AllResults_{FileIdentifiers.ChimeraBreakdownComparisonFigure}{pepLabel}s");
            peptideChart.SavePNG(peptideOutPath, null, width, DefaultHeight);
        }

        public static void PlotBulkResultChimeraBreakDown_TargetDecoy(this AllResults allResults)
        {
            var selector = allResults.First().First().IsTopDown
                ? AcceptableConditionsToPlotChimeraBreakdownTopDown
                : AcceptableConditionsToPlotFDRComparisonResults;
            bool isTopDown = allResults.First().First().IsTopDown;
            var smLabel = isTopDown ? "PrSM" : "PSM";
            var pepLabel = isTopDown ? "Proteoform" : "Peptide";
            var results = allResults.SelectMany(z => z.Results
                           .Where(p => p is MetaMorpheusResult && selector.Contains(p.Condition))
                           .SelectMany(p => ((MetaMorpheusResult)p).ChimeraBreakdownFile.Results))
                .ToList();
            var psmChart =
                results.GetChimeraBreakDownStackedColumn_TargetDecoy(ChimeraBreakdownType.Psm, isTopDown, out int width);
            var psmOutPath = Path.Combine(allResults.GetFigureDirectory(),
                                              $"AllResults_{FileIdentifiers.ChimeraBreakdownTargetDecoy}_{smLabel}");
            psmChart.SavePNG(psmOutPath, null, width, DefaultHeight);

            var peptideChart =
                results.GetChimeraBreakDownStackedColumn_TargetDecoy(ChimeraBreakdownType.Peptide, isTopDown, out width);
            var peptideOutPath = Path.Combine(allResults.GetFigureDirectory(),
                                              $"AllResults_{FileIdentifiers.ChimeraBreakdownTargetDecoy}_{pepLabel}");
            peptideChart.SavePNG(peptideOutPath, null, width, DefaultHeight);
        }

        #region Validation

        // Too big to export
        public static void PlotBulkResultRetentionTimePredictions(this AllResults allResults)
        {
            var retentionTimePredictions = allResults.CellLineResults
                .SelectMany(p => p.Where(p => AcceptableConditionsToPlotFDRComparisonResults.Contains(p.Condition))
                    .OrderBy(p => ((MetaMorpheusResult)p).RetentionTimePredictionFile.First())
                    .Select(p => ((MetaMorpheusResult)p).RetentionTimePredictionFile))
                    .ToList();

            var chronologer = retentionTimePredictions
                .SelectMany(p => p.Where(m => m.ChronologerPrediction != 0 && m.PeptideModSeq != ""))
                .ToList();
            var ssrCalc = retentionTimePredictions
                .SelectMany(p => p.Where(m => m.SSRCalcPrediction is not 0 or double.NaN or -1))
                .ToList();

            var chronologerPlot = Chart.Combine(new[]
                {
                    Chart2D.Chart.Scatter<double, double, string>(
                        chronologer.Where(p => !p.IsChimeric).Select(p => p.RetentionTime),
                        chronologer.Where(p => !p.IsChimeric).Select(p => p.ChronologerPrediction), StyleParam.Mode.Markers,
                        "No Chimeras", MarkerColor: ConditionToColorDictionary["No Chimeras"]),
                    Chart2D.Chart.Scatter<double, double, string>(
                        chronologer.Where(p => p.IsChimeric).Select(p => p.RetentionTime),
                        chronologer.Where(p => p.IsChimeric).Select(p => p.ChronologerPrediction), StyleParam.Mode.Markers,
                        "Chimeras", MarkerColor: ConditionToColorDictionary["Chimeras"])
                })
                .WithTitle($"All Results Chronologer Predicted HI vs Retention Time (1% Peptides)")
                .WithXAxisStyle(Title.init("Retention Time"))
                .WithYAxisStyle(Title.init("Chronologer Prediction"))
                .WithLayout(DefaultLayoutWithLegend)
                .WithSize(1000, 600);


            PuppeteerSharpRendererOptions.launchOptions.Timeout = 0;
            string outpath = Path.Combine(allResults.GetFigureDirectory(), $"AllResults_{FileIdentifiers.ChronologerFigure}_Aggregated");
            chronologerPlot.SavePNG(outpath, ExportEngine.PuppeteerSharp, 1000, 600);

            var ssrCalcPlot = Chart.Combine(new[]
                {
                    Chart2D.Chart.Scatter<double, double, string>(
                        ssrCalc.Where(p => !p.IsChimeric).Select(p => p.RetentionTime),
                        ssrCalc.Where(p => !p.IsChimeric).Select(p => p.SSRCalcPrediction), StyleParam.Mode.Markers,
                        "No Chimeras", MarkerColor: ConditionToColorDictionary["No Chimeras"]),
                    Chart2D.Chart.Scatter<double, double, string>(
                        ssrCalc.Where(p => p.IsChimeric).Select(p => p.RetentionTime),
                        ssrCalc.Where(p => p.IsChimeric).Select(p => p.SSRCalcPrediction), StyleParam.Mode.Markers,
                        "Chimeras", MarkerColor: ConditionToColorDictionary["Chimeras"])
                })
                .WithTitle($"All Results SSRCalc3 Predicted HI vs Retention Time (1% Peptides)")
                .WithXAxisStyle(Title.init("Retention Time"))
                .WithYAxisStyle(Title.init("SSRCalc3 Prediction"))
                .WithLayout(DefaultLayoutWithLegend)
                .WithSize(1000, 600);
            outpath = Path.Combine(allResults.GetFigureDirectory(), $"AllResults_{FileIdentifiers.SSRCalcFigure}_Aggregated");
            ssrCalcPlot.SavePNG(outpath, null, 1000, 600);
        }

        // too big to export
        public static void PlotStackedRetentionTimePredictions(this AllResults allResults)
        {
            var results = allResults.Select(p => p.GetCellLineRetentionTimePredictions()).ToList();

            var chronologer = Chart.Grid(results.Select(p => p.Chronologer),
                    results.Count(), 1, Pattern: StyleParam.LayoutGridPattern.Independent, YGap: 0.2,
                    XSide: StyleParam.LayoutGridXSide.Bottom)
                .WithTitle("Chronologer Predicted HI vs Retention Time (1% Peptides)")
                .WithSize(1000, 400 * results.Count())
                .WithXAxisStyle(Title.init("Retention Time"))
                .WithYAxisStyle(Title.init("Chronologer Prediction"))
                .WithLayout(DefaultLayoutWithLegend);
            string outpath = Path.Combine(allResults.GetFigureDirectory(), $"AllResults_{FileIdentifiers.ChronologerFigure}_Stacked");
            chronologer.SavePNG(outpath, ExportEngine.PuppeteerSharp, 1000, 400 * results.Count());

            var ssrCalc = Chart.Grid(results.Select(p => p.SSRCalc3),
                    results.Count(), 1, Pattern: StyleParam.LayoutGridPattern.Independent, YGap: 0.2)
                .WithTitle("SSRCalc3 Predicted HI vs Retention Time (1% Peptides)")
                .WithSize(1000, 400 * results.Count())
                .WithXAxisStyle(Title.init("Retention Time"))
                .WithYAxisStyle(Title.init("SSRCalc3 Prediction"))
                .WithLayout(DefaultLayoutWithLegend);
            outpath = Path.Combine(allResults.GetFigureDirectory(), $"AllResults_{FileIdentifiers.SSRCalcFigure}_Stacked");
            ssrCalc.SavePNG(outpath, null, 1000, 400 * results.Count());
        }

        public static void PlotStackedIndividualFileComparison(this AllResults allResults)
        {
            int width = 0;
            int height = 0;

            double heightScaler = allResults.First().First().IsTopDown ? 1.5 : 2.5;
            var title = allResults.First().First().IsTopDown ? "PrSMs" : "Peptides";
            var chart = Chart.Grid(
                    allResults.Select(p => p.GetIndividualFileResults(out width, out height).WithYAxisStyle(Title.init(p.CellLine))),
                    allResults.Count(), 1, Pattern: StyleParam.LayoutGridPattern.Independent, YGap: 0.2)
                .WithTitle($"Individual File Comparison 1% {title}")
                .WithSize(width, (int)(height * allResults.Count() / heightScaler))
                .WithLayout(DefaultLayoutWithLegend);
            string outpath = Path.Combine(allResults.GetFigureDirectory(), $"AllResults_{FileIdentifiers.IndividualFileComparisonFigure}_Stacked");


            chart.SavePNG(outpath, null, width, (int)(height * allResults.Count() / heightScaler));
        }

        public static void PlotStackedSpectralSimilarity(this AllResults allResults)
        {

            var chart = Chart.Grid(
                allResults.Select(p => p.GetCellLineSpectralSimilarity().WithYAxisStyle(Title.init(p.CellLine))),
                                4, 3, Pattern: StyleParam.LayoutGridPattern.Independent, YGap: 0.2)
                .WithTitle("Spectral Angle Distribution (1% Peptides)")
                .WithSize(1000, 800)
                .WithLayout(DefaultLayout);
            string outpath = Path.Combine(allResults.GetFigureDirectory(), $"AllResults_{FileIdentifiers.SpectralAngleFigure}_Stacked");
            chart.SavePNG(outpath, null, 1000, 800);
        }

        public static void PlotAggregatedSpectralSimilarity(this AllResults allResults)
        {
            var results = allResults.CellLineResults.SelectMany(n => n
                .Where(p => AcceptableConditionsToPlotFDRComparisonResults.Contains(p.Condition))
                .OrderBy(p => ((MetaMorpheusResult)p).RetentionTimePredictionFile.First())
                .SelectMany(p => ((MetaMorpheusResult)p).RetentionTimePredictionFile.Results.Where(m => m.SpectralAngle is not -1 or double.NaN)))
                .ToList();

            var violin = Chart.Combine(new[]
                {
                    Chart.Violin<string, double, string>(
                        new Plotly.NET.CSharp.Optional<IEnumerable<string>>(
                            Enumerable.Repeat("Chimeras", results.Count(p => p.IsChimeric)), true),
                        new Plotly.NET.CSharp.Optional<IEnumerable<double>>(
                            results.Where(p => p.IsChimeric).Select(p => p.SpectralAngle), true),
                        null, MarkerColor: ConditionToColorDictionary["Chimeras"],
                        MeanLine: MeanLine.init(true, ConditionToColorDictionary["Chimeras"]),
                        ShowLegend: false),
                    Chart.Violin<string, double, string>(
                        new Plotly.NET.CSharp.Optional<IEnumerable<string>>(
                            Enumerable.Repeat("No Chimeras", results.Count(p => !p.IsChimeric)), true),
                        new Plotly.NET.CSharp.Optional<IEnumerable<double>>(
                            results.Where(p => !p.IsChimeric).Select(p => p.SpectralAngle), true),
                        null, MarkerColor: ConditionToColorDictionary["No Chimeras"],
                        MeanLine: MeanLine.init(true, ConditionToColorDictionary["No Chimeras"]),
                        ShowLegend: false)
                })
                .WithTitle($"All Results Spectral Angle Distribution (1% Peptides)")
                .WithYAxisStyle(Title.init("Spectral Angle"))
                .WithLayout(DefaultLayout)
                .WithSize(1000, 600);
            string outpath = Path.Combine(allResults.GetFigureDirectory(),
                $"AllResults_{FileIdentifiers.SpectralAngleFigure}_Aggregated");
            violin.SavePNG(outpath);
        }

        #endregion

        #endregion

        #region Generic

        internal static GenericChart.GenericChart GetChimeraBreakDownStackedColumn(this List<ChimeraBreakdownRecord> results, ChimeraBreakdownType type, bool isTopDown, out int width)
        {
            (int IdPerSpec, int Parent, int UniqueProtein, int UniqueForms, int Decoys)[] data = results.Where(p => p.Type == type)
                .GroupBy(p => p.IdsPerSpectra)
                .OrderBy(p => p.Key)
                .Select(p => 
                    (
                        p.Key, 
                        p.Sum(m => m.Parent), 
                        p.Sum(m => m.UniqueProteins), 
                        p.Sum(m => m.UniqueForms),
                        p.Sum(m => m.DecoyCount))
                    )
                .ToArray();
            var keys = data.Select(p => p.IdPerSpec).ToArray();
            width = Math.Max(600, 50 * data.Length);
            var form = isTopDown ? "Proteoform" : "Peptidoform";
            string title = isTopDown ? type == ChimeraBreakdownType.Psm ? "PrSM" : "Proteoform" :
                type == ChimeraBreakdownType.Psm ? "PSM" : "Peptide";
            var title2 = results.Select(p => p.Dataset).Distinct().Count() == 1 ? results.First().Dataset : "All Results";
            var chart = Chart.Combine(new[]
                {
                    Chart.StackedColumn<int, int, string>(data.Select(p => p.Parent), keys, "Isolated Species",
                        MarkerColor: ConditionToColorDictionary["Isolated Species"], MultiText: data.Select(p => p.Parent.ToString()).ToArray()),
                    Chart.StackedColumn<int, int, string>(data.Select(p => p.Decoys), keys, "Decoys",
                        MarkerColor: ConditionToColorDictionary["Decoys"], MultiText: data.Select(p => p.Decoys.ToString()).ToArray()),
                    Chart.StackedColumn<int, int, string>(data.Select(p => p.UniqueProtein), keys, $"Unique Protein",
                        MarkerColor: ConditionToColorDictionary["Unique Protein"], MultiText: data.Select(p => p.UniqueProtein.ToString()).ToArray()),
                    Chart.StackedColumn<int, int, string>(data.Select(p => p.UniqueForms), keys, $"Unique {form}",
                        MarkerColor: ConditionToColorDictionary[$"Unique {form}"], MultiText: data.Select(p => p.UniqueForms.ToString()).ToArray()),
                })
                .WithLayout(DefaultLayoutWithLegend)
                .WithTitle($"{title2} 1% {title} Identifications per Spectra")
                .WithXAxisStyle(Title.init("IDs per Spectrum"))
                .WithYAxis(LinearAxis.init<int, int, int,int, int, int>(AxisType: StyleParam.AxisType.Log))
                .WithYAxisStyle(Title.init("Count"))
                .WithSize(width, DefaultHeight);
            return chart;
        }

        internal static GenericChart.GenericChart GetChimeraBreakDownStackedColumn_TargetDecoy(
            this List<ChimeraBreakdownRecord> results, ChimeraBreakdownType type, bool isTopDown, out int width)
        {
            (int IdPerSpec, int Parent, double Targets, double Decoys)[] data = results.Where(p => p.Type == type)
                .GroupBy(p => p.IdsPerSpectra)
                .OrderBy(p => p.Key)
                .Select(p =>
                    (
                        p.Key,
                        p.Sum(m => m.Parent),
                        p.Average(m => m.TargetCount / (double)(m.TargetCount + m.DecoyCount) * 100),
                        p.Average(m => m.DecoyCount / (double)(m.TargetCount + m.DecoyCount) * 100))
                )
                .ToArray();
            var keys = data.Select(p => p.IdPerSpec).ToArray();
            width = Math.Max(600, 50 * data.Length);
            var form = isTopDown ? "Proteoform" : "Peptidoform";
            string title = isTopDown ? type == ChimeraBreakdownType.Psm ? "PrSM" : "Proteoform" :
                type == ChimeraBreakdownType.Psm ? "PSM" : "Peptide";
            var title2 = results.Select(p => p.Dataset).Distinct().Count() == 1 ? results.First().Dataset : "All Results";
            var chart = Chart.Combine(new[]
                {
                    Chart.StackedColumn<double, int, string>(data.Select(p => p.Targets), keys, "Targets",
                        MarkerColor: ConditionToColorDictionary["Targets"], MultiText: data.Select(p => Math.Round(p.Targets, 1).ToString()).ToArray()),
                    Chart.StackedColumn<double, int, string>(data.Select(p => p.Decoys), keys, $"Decoys",
                        MarkerColor: ConditionToColorDictionary["Decoys"], MultiText: data.Select(p => Math.Round(p.Decoys, 1).ToString()).ToArray()),
                })
                .WithLayout(DefaultLayoutWithLegend)
                .WithTitle($"{title2} 1% {title} Identifications per Spectra")
                .WithXAxisStyle(Title.init("IDs per Spectrum"))
                .WithYAxis(LinearAxis.init<int, int, int, int, int, int>(AxisType: StyleParam.AxisType.Linear))
                .WithYAxisStyle(Title.init("Percent"))
                .WithSize(width, DefaultHeight);
            return chart;
        }

        #endregion

        private static string GetFigureDirectory(this AllResults allResults)
        {
            var directory = Path.Combine(allResults.DirectoryPath, "Figures");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return directory;
        }

        private static string GetFigureDirectory(this CellLineResults cellLine)
        {
            var directory = Path.Combine(Path.GetDirectoryName(cellLine.DirectoryPath)!, "Figures");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
