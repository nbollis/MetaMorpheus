using System.Windows.Controls;
using OxyPlot;

namespace MetaMorpheusGUI;

/// <summary>
/// Interaction logic for PhylogeneticTreeControl.xaml
/// </summary>
public partial class PhylogeneticTreeControl : UserControl
{
    public PhylogeneticTreeControl()
    {
        InitializeComponent();

        var controller = new PlotController();
        controller.UnbindAll();
        controller.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
        controller.BindMouseWheel(PlotCommands.ZoomWheel);
        controller.BindMouseDown(OxyMouseButton.Right, PlotCommands.ZoomRectangle);
        controller.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.Control, PlotCommands.ZoomRectangle);
        controller.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.Alt, PlotCommands.ResetAt);
        controller.Bind(new OxyMouseDownGesture(OxyMouseButton.Left, OxyModifierKeys.None, 2), PlotCommands.ResetAt);
        PhylogeneticTreePlotView.Controller = controller;
    }
}
