using System;
using System.Collections.Generic;
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


namespace Test.ChimeraPaper
{
    public static class Plotting
    {
        #region Filters

        public static string[] AcceptableConditionsToPlotIndividualFileComparison =
        {
            "MsFragger", "MsFraggerDDA+", 
            "MetaMorpheusWithLibrary", "MetaMorpheusNoChimerasWithLibrary",
            "ReviewdDatabaseNoPhospho_MsFraggerDDA", "ReviewdDatabaseNoPhospho_MsFraggerDDA+", "ReviewdDatabaseNoPhospho_MsFragger" // check if this makes a difference for fragger
        };

        public static string[] AcceptableConditionsToPlotInternalMMComparison =
        {
            "MetaMorpheusWithLibrary", "MetaMorpheusNoChimerasWithLibrary"
        };

        public static string[] AcceptableConditionsToPlotBulkResultComparison =
        {
            "MsFragger", "MsFraggerDDA+", 
            "MetaMorpheusWithLibrary", "MetaMorpheusNoChimerasWithLibrary", 
            "ReviewdDatabaseNoPhospho_MsFraggerDDA+", "ReviewdDatabaseNoPhospho_MsFragger"
        };

        public static string[] AcceptableConditionsToPlotFDRComparisonResults =
        {
            "MetaMorpheusWithLibrary"
        };

        public static Dictionary<string, Color> ConditionToColorDictionary = new()
        {
            {"MetaMorpheusWithLibrary", Color.fromKeyword(ColorKeyword.Purple) },
            {"MetaMorpheusNoChimerasWithLibrary", Color.fromKeyword(ColorKeyword.Plum) },
            {"Chimeras", Color.fromKeyword(ColorKeyword.Purple) },
            {"No Chimeras", Color.fromKeyword(ColorKeyword.Plum) },
            {"MsFragger", Color.fromKeyword(ColorKeyword.LightAkyBlue) },
            {"MsFraggerDDA+", Color.fromKeyword(ColorKeyword.RoyalBlue) },
            {"MetaMorpheusFraggerEquivalentChimeras_IndividualFiles", Color.fromKeyword(ColorKeyword.SpringGreen) },
            {"MetaMorpheusFraggerEquivalentNoChimeras_IndividualFiles", Color.fromKeyword(ColorKeyword.Green) },
            {"ReviewdDatabaseNoPhospho_MsFraggerDDA", Color.fromKeyword(ColorKeyword.IndianRed) },
            {"ReviewdDatabaseNoPhospho_MsFragger", Color.fromKeyword(ColorKeyword.IndianRed) },
            {"ReviewdDatabaseNoPhospho_MsFraggerDDA+", Color.fromKeyword(ColorKeyword.Red) },
        };

        public static Dictionary<string, string> FileNameConversionDictionary = new()
        {
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
        };

        public static IEnumerable<string> ConvertFileNames(this IEnumerable<string> fileNames)
        {
            return fileNames.Select(p => p.Split('-')[0])
                .Select(p => FileNameConversionDictionary.ContainsKey(p) ? FileNameConversionDictionary[p] : p);
        }

        public static Dictionary<string, string> ConitionNameConversionDictionary = new()
        {
            { "MetaMorpheusWithLibrary", "MetaMorpheus" },
            { "MetaMorpheusNoChimerasWithLibrary", "MetaMorpheus No Chimeras" },
            { "ReviewdDatabaseNoPhospho_MsFragger", "NewParams_MsFragger" },
            { "ReviewdDatabaseNoPhospho_MsFraggerDDA", "NewParams_MsFragger" },
            { "ReviewdDatabaseNoPhospho_MsFraggerDDA+", "NewParams_MsFraggerDDA+" }
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
            var individualFileResults = cellLine.Results.Select(p => p.IndividualFileComparisonFile)
                .Where(p => AcceptableConditionsToPlotIndividualFileComparison.Contains(p.First().Condition))
                .OrderBy(p => p.First().Condition)
                .ToList();
            var labels = individualFileResults.SelectMany(p => p.Results.Select(m => m.FileName))
                .ConvertFileNames().Distinct().ToList();

            var chart = Chart.Combine(individualFileResults.Select(p =>
                Chart2D.Chart.Column<int, string, string, int, int>(p.Select(m => m.OnePercentPeptideCount), labels, null,
                    p.Results.First().Condition.ConvertConditionName(), MarkerColor: ConditionToColorDictionary[p.First().Condition])
            ));
            width = 50 * labels.Count + 10 * individualFileResults.Count;
            height = 600;
            chart.WithTitle($"{cellLine.CellLine} 1% FDR Peptides")
                .WithXAxisStyle(Title.init("File"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend)
                .WithSize(width, height);
            return chart;
        }

        public static void PlotCellLineRetentionTimePredictions(this CellLineResults cellLine)
        {
            var plots = cellLine.GetCellLineRetentionTimePredictions();
            string outPath = Path.Combine(cellLine.GetFigureDirectory(), $"{FileIdentifiers.ChronologerFigure}_{cellLine.CellLine}");
            plots.Chronologer.SavePNG(outPath, null, 1000, 600);

            outPath = Path.Combine(cellLine.GetFigureDirectory(), $"{FileIdentifiers.SSRCalcFigure}_{cellLine.CellLine}");
            plots.SSRCalc3.SavePNG(outPath, null, 1000, 600);
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
                .WithSize(1000, 600);

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
                .WithSize(1000, 600);
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
            var results = allResults.CellLineResults.SelectMany(p => p.BulkResultCountComparisonFile.Results)
                .Where(p => AcceptableConditionsToPlotInternalMMComparison.Contains(p.Condition))
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
            psmChart.WithTitle("MetaMorpheus 1% FDR Psms")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);
            string psmOutpath = Path.Combine(allResults.GetFigureDirectory(), "InternalMetaMorpheusComparison_Psms");
            psmChart.SavePNG(psmOutpath);

            var peptideChart = Chart.Combine(new[]
            {
                Chart2D.Chart.Column<int, string, string, int, int>(noChimeras.Select(p => p.OnePercentPeptideCount),
                    labels, null, "No Chimeras", MarkerColor: ConditionToColorDictionary[noChimeras.First().Condition]),
                Chart2D.Chart.Column<int, string, string, int, int>(withChimeras.Select(p => p.OnePercentPeptideCount),
                    labels, null, "Chimeras", MarkerColor: ConditionToColorDictionary[withChimeras.First().Condition])
            });
            peptideChart.WithTitle("MetaMorpheus 1% FDR Peptides")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);
            string peptideOutpath = Path.Combine(allResults.GetFigureDirectory(), "InternalMetaMorpheusComparison_Peptides");
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
            var results = allResults.CellLineResults.SelectMany(p => p.BulkResultCountComparisonFile.Results)
                .Where(p => AcceptableConditionsToPlotBulkResultComparison.Contains(p.Condition))
                .ToList();
            var labels = results.Select(p => p.DatasetName).Distinct().ConvertConditionNames().ToList();

            var psmCharts = new List<GenericChart.GenericChart>();
            var peptideCharts = new List<GenericChart.GenericChart>();
            var proteinCharts = new List<GenericChart.GenericChart>();
            foreach (var condition in results.Select(p => p.Condition).Distinct())
            {
                var conditionSpecificResults = results.Where(p => p.Condition == condition).ToList();

                psmCharts.Add(Chart2D.Chart.Column<int, string, string, int, int>(
                    conditionSpecificResults.Select(p => p.OnePercentPsmCount), labels, null, condition,
                    MarkerColor: ConditionToColorDictionary[condition]));
                peptideCharts.Add(Chart2D.Chart.Column<int, string, string, int, int>(
                    conditionSpecificResults.Select(p => p.OnePercentPeptideCount), labels, null, condition,
                    MarkerColor: ConditionToColorDictionary[condition]));
                proteinCharts.Add(Chart2D.Chart.Column<int, string, string, int, int>(
                    conditionSpecificResults.Select(p => p.OnePercentProteinGroupCount), labels, null, condition,
                    MarkerColor: ConditionToColorDictionary[condition]));
            }

            var psmChart = Chart.Combine(psmCharts).WithTitle("1% FDR Psms")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);
            var peptideChart = Chart.Combine(peptideCharts).WithTitle("1% FDR Peptides")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);
            var proteinChart = Chart.Combine(proteinCharts).WithTitle("1% FDR Proteins")
                .WithXAxisStyle(Title.init("Cell Line"))
                .WithYAxisStyle(Title.init("Count"))
                .WithLayout(DefaultLayoutWithLegend);

            var psmPath = Path.Combine(allResults.GetFigureDirectory(), "BulkResultComparison_Psms");
            var peptidePath = Path.Combine(allResults.GetFigureDirectory(), "BulkResultComparison_Peptides");
            var proteinpath = Path.Combine(allResults.GetFigureDirectory(), "BulkResultComparison_Proteins");
            psmChart.SavePNG(psmPath);
            peptideChart.SavePNG(peptidePath);
            proteinChart.SavePNG(proteinpath);
        }

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
            var chart = Chart.Grid(
                    allResults.Select(p => p.GetIndividualFileResults(out width, out height).WithYAxisStyle(Title.init(p.CellLine))),
                    allResults.Count(), 1, Pattern: StyleParam.LayoutGridPattern.Independent, YGap: 0.2 )
                .WithTitle("Individual File Comparison")
                .WithSize(width, (int)(height * allResults.Count() / 2.5))
                .WithLayout(DefaultLayoutWithLegend);
            string outpath = Path.Combine(allResults.GetFigureDirectory(), $"AllResults_{FileIdentifiers.IndividualFileComparisonFigure}_Stacked");
            chart.SavePNG(outpath, null, width, (int)(height * allResults.Count() / 2.5));
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
