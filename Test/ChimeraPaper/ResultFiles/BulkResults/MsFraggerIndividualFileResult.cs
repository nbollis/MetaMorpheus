namespace Test.ChimeraPaper.ResultFiles;

public class MsFraggerIndividualFileResult
{
    public string DirectoryPath { get; set; }

    private string _psmPath;
    private MsFraggerPsmFile _psmFile;
    public MsFraggerPsmFile PsmFile => _psmFile ??= new MsFraggerPsmFile(_psmPath);

    private string _peptidePath;
    private MsFraggerPeptideFile _peptideFile;
    public MsFraggerPeptideFile PeptideFile => _peptideFile ??= new MsFraggerPeptideFile(_peptidePath);

    private string _proteinPath;
    private MsFraggerProteinFile _proteinFile;
    public MsFraggerProteinFile ProteinFile => _proteinFile ??= new MsFraggerProteinFile(_proteinPath);


    public MsFraggerIndividualFileResult(string directoryPath)
    {
        DirectoryPath = directoryPath;
        _psmPath = System.IO.Path.Combine(DirectoryPath, "psm.tsv");
        _peptidePath = System.IO.Path.Combine(DirectoryPath, "peptide.tsv");
        _proteinPath = System.IO.Path.Combine(DirectoryPath, "protein.tsv");
    }
}