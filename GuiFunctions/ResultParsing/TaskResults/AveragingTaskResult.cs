﻿using MassSpectrometry;
using MzLibSpectralAveraging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskLayer;

namespace GuiFunctions
{
    public class AveragingTaskResult : TaskResults
    {
        #region Private Properties

        private string[] outputSpectraPaths;
        private string[] outputTomlPaths;
        private Dictionary<string, List<MsDataScan>> outputSpectra;

        #endregion


        #region Public Properties

        public Dictionary<string, List<MsDataScan>> OutputSpectra
        {
            get
            {
                if (outputSpectra.Any()) return outputSpectra;
                foreach (var path in outputSpectraPaths)
                {
                    var fileName = Path.GetFileName(path);
                    var scans = SpectraFileHandler.LoadAllScansFromFile(path);
                    outputSpectra.Add(fileName, scans);
                }
                return outputSpectra;
            }
        }

        #endregion

        #region Constructor

        public AveragingTaskResult(string taskDirectory, string name) : base(taskDirectory, MyTask.Averaging, name)
        {
            outputSpectra = new();
            var files = Directory.GetFiles(taskDirectory);
            outputSpectraPaths = files.Where(p => p.Contains(".mzML")).OrderBy(p => p).ToArray();
            outputTomlPaths = files.Where(p => p.Contains(".toml")).OrderBy(p => p).ToArray();
        }

        #endregion

        #region Processing Methods



        #endregion

        #region ITsv Members



        #endregion







    }
}