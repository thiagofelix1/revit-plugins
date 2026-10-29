using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Nice3point.Revit.Toolkit.External;
using Spk.AddIn.Modelagem;
using TextBox = System.Windows.Controls.TextBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Spk.AddIn.Commands;

/// <summary>
/// Marca a entrada e o nome de um setor numa rede sem VGA desenhada (<see cref="EntradasDeSetor" />): o projetista clica
/// no tubo por onde a água chega e escreve o nome. Na rede que já tem marca, oferece trocar o nome ou tirar a marca.
/// </summary>
[UsedImplicitly]
[Transaction(TransactionMode.Manual)]
public class MarcarSetorCommand : ExternalCommand
{
    const string Titulo = "SPK — Marcar setor";

    public override void Execute()
    {
        var ui = Application.ActiveUIDocument;
        if (ui?.Document is not { IsFamilyDocument: false } doc)
        {
            TaskDialog.Show(Titulo, "Abra um projeto antes.");
            return;
        }

        Reference referencia;
        try
        {
            referencia = ui.Selection.PickObject(ObjectType.Element, new SoTubos(),
                "Clique no tubo por onde a água chega ao setor (a descida da VGA), perto da ponta de entrada");
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return;
        }
        var tubo = (Pipe)doc.GetElement(referencia);
        var perto = referencia.GlobalPoint ?? ((LocationCurve)tubo.Location).Curve.Evaluate(0.5, true);
        var atual = EntradasDeSetor.DaRede(doc, tubo);
        var sugestao = atual?.Nome ?? $"VGA {EntradasDeSetor.Ler(doc).Count + 1:00}";

        var janela = new NomeDoSetorJanela(sugestao, atual is not null);
        if (janela.ShowDialog() != true) return;

        using var t = new Transaction(doc, Titulo);
        t.Start();
        try
        {
            if (janela.Tirar)
            {
                var saiu = EntradasDeSetor.Desmarcar(doc, tubo);
                t.Commit();
                TaskDialog.Show(Titulo, saiu is null
                    ? "Esta rede não tinha marca."
                    : $"A marca \"{saiu}\" saiu. A rede volta a ser um setor sem nome, com a entrada na ponta aberta do tubo mais grosso.");
                return;
            }
            var feito = EntradasDeSetor.Marcar(doc, tubo, perto, janela.Nome);
            t.Commit();
            var e = feito.Entrada;
            new TaskDialog(Titulo)
            {
                MainInstruction = $"Setor \"{e.Nome}\": {feito.Bicos} bico(s)",
                MainContent = $"A água entra pela ponta do tubo a {e.Z:0.00} m de cota. Calcular, Diâmetros, o memorial e as folhas " +
                              "usam essa entrada e esse nome." +
                              (feito.Substituida is { } antes ? $"\n\nA marca anterior desta rede (\"{antes}\") foi trocada." : "") +
                              (feito.Aviso is { } aviso ? "\n\n" + aviso : ""),
            }.Show();
        }
        catch (InvalidOperationException ex)
        {
            t.RollBack();
            TaskDialog.Show(Titulo, ex.Message);
        }
    }

    sealed class SoTubos : ISelectionFilter
    {
        public bool AllowElement(Element elemento) => elemento is Pipe;
        public bool AllowReference(Reference referencia, XYZ ponto) => false;
    }
}

/// <summary>O nome do setor, com Marcar e, se a rede já tem marca, Tirar a marca.</summary>
sealed class NomeDoSetorJanela : Window
{
    readonly TextBox caixa;

    public string Nome => caixa.Text;
    public bool Tirar { get; private set; }

    public NomeDoSetorJanela(string sugestao, bool jaMarcada)
    {
        Title = "SPK — Marcar setor";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        FontSize = 13;

        caixa = new TextBox { Text = sugestao, Margin = new Thickness(0, 4, 0, 0), Padding = new Thickness(4, 3, 4, 3) };
        var marcar = new Button { Content = "Marcar", Width = 100, Height = 30, IsDefault = true, FontWeight = FontWeights.Bold };
        marcar.Click += (_, _) => DialogResult = true;
        var cancelar = new Button { Content = "Cancelar", Width = 100, Height = 30, Margin = new Thickness(0, 0, 8, 0), IsCancel = true };
        var botoes = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        if (jaMarcada)
        {
            var tirar = new Button { Content = "Tirar a marca", Width = 110, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            tirar.Click += (_, _) =>
            {
                Tirar = true;
                DialogResult = true;
            };
            botoes.Children.Add(tirar);
        }
        botoes.Children.Add(cancelar);
        botoes.Children.Add(marcar);

        var painel = new StackPanel { Margin = new Thickness(16) };
        painel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x37, 0x47, 0x4F)),
            Text = jaMarcada
                ? "Esta rede já tem marca. Troque o nome, marque de novo para mudar a entrada para a ponta clicada, ou tire a marca."
                : "Nome do setor, como vai aparecer no cálculo, no memorial e nas folhas (por exemplo, VGA 01 ou VGA 02 - Térreo).",
        });
        painel.Children.Add(new TextBlock { Text = "Nome", Margin = new Thickness(0, 12, 0, 0) });
        painel.Children.Add(caixa);
        painel.Children.Add(botoes);
        Content = painel;
        Loaded += (_, _) =>
        {
            caixa.Focus();
            caixa.SelectAll();
        };
    }
}
