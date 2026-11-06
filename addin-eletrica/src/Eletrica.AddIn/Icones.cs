using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Freezable = System.Windows.Freezable;
using Point = System.Windows.Point;

namespace Eletrica.AddIn;

/// <summary>Ícones da faixa de opções desenhados num quadro de 32×32 e rasterizados no tamanho pedido.</summary>
static class Icones
{
    static readonly Brush Vermelho = Congelado(new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)));
    static readonly Brush Grafite = Congelado(new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F)));

    public static ImageSource Gerar(Action<DrawingContext> desenhar, int tamanho)
    {
        const double escala = 2;
        const double margem = 2;
        var pixels = (int)(tamanho * escala);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(pixels / 32.0, pixels / 32.0));
            var interno = (32 - 2 * margem) / 32.0;
            dc.PushTransform(new TranslateTransform(margem, margem));
            dc.PushTransform(new ScaleTransform(interno, interno));
            desenhar(dc);
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(pixels, pixels, 96 * escala, 96 * escala, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public static void Lupa(DrawingContext dc)
    {
        dc.DrawEllipse(Brushes.White, Caneta(Grafite, 3), new Point(13, 13), 9, 9);
        dc.DrawEllipse(Vermelho, null, new Point(13, 13), 3.5, 3.5);
        dc.DrawLine(Caneta(Vermelho, 4.5), new Point(20, 20), new Point(28.5, 28.5));
    }

    public static void Executar(DrawingContext dc)
    {
        var triangulo = Geometry.Parse("M 9,4 L 28,16 L 9,28 Z");
        dc.DrawGeometry(Vermelho, Caneta(Vermelho, 2), triangulo);
    }

    public static void Documento(DrawingContext dc)
    {
        dc.DrawGeometry(Brushes.White, Caneta(Grafite, 2.5), Geometry.Parse("M 7,3 L 19,3 L 26,10 L 26,29 L 7,29 Z"));
        dc.DrawGeometry(null, Caneta(Grafite, 2), Geometry.Parse("M 19,3 L 19,10 L 26,10"));
        var linha = Caneta(Vermelho, 2.5);
        foreach (var y in new[] { 15.0, 19.5, 24.0 })
            dc.DrawLine(linha, new Point(11, y), new Point(22, y));
    }

    public static void Formulario(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, Caneta(Grafite, 2.5), new System.Windows.Rect(7, 4, 18, 24));
        var linha = Caneta(Vermelho, 2);
        foreach (var y in new[] { 11.0, 16.0, 21.0 })
            dc.DrawLine(linha, new Point(11, y), new Point(21, y));
    }

    public static void Folha(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, Caneta(Grafite, 2), new System.Windows.Rect(6, 3, 18, 14));
        dc.DrawRectangle(null, Caneta(Vermelho, 1.6), new System.Windows.Rect(9, 6, 12, 7));
        dc.DrawRectangle(Brushes.White, Caneta(Grafite, 2), new System.Windows.Rect(8, 12, 18, 14));
    }

    public static void Malha(DrawingContext dc)
    {
        var caneta = Caneta(Grafite, 2);
        dc.DrawLine(caneta, new Point(6, 16), new Point(26, 16));
        dc.DrawLine(caneta, new Point(8, 8), new Point(8, 24));
        foreach (var x in new[] { 8.0, 14.0, 20.0, 26.0 })
            dc.DrawEllipse(Vermelho, null, new Point(x, 16), 2.2, 2.2);
    }

    public static void TracarRamal(DrawingContext dc)
    {
        dc.DrawLine(Caneta(Grafite, 3), new Point(6, 3), new Point(6, 29));
        dc.DrawLine(Caneta(Vermelho, 2.6), new Point(6, 14), new Point(29, 14));
        foreach (var x in new[] { 13.0, 20.0, 27.0 })
        {
            dc.DrawLine(Caneta(Vermelho, 1.8), new Point(x, 14), new Point(x, 20));
            dc.DrawEllipse(Vermelho, null, new Point(x, 22.5), 2.4, 2.4);
        }
    }

    public static void BicosAmbiente(DrawingContext dc)
    {
        // Planta de um ambiente com a grade de bicos.
        dc.DrawRectangle(Brushes.White, Caneta(Grafite, 2.4), new System.Windows.Rect(3, 5, 26, 22));
        foreach (var x in new[] { 9.5, 22.5 })
            foreach (var y in new[] { 10.5, 21.5 })
                dc.DrawEllipse(Vermelho, null, new Point(x, y), 2.6, 2.6);
    }

    public static void Edificio(DrawingContext dc)
    {
        // Corte de um prédio: lajes, paredes e um forro em vermelho.
        var parede = Caneta(Grafite, 2.2);
        dc.DrawLine(Caneta(Grafite, 3), new Point(3, 28), new Point(29, 28));
        dc.DrawLine(parede, new Point(5, 28), new Point(5, 6));
        dc.DrawLine(parede, new Point(27, 28), new Point(27, 6));
        dc.DrawLine(Caneta(Grafite, 3), new Point(3, 6), new Point(29, 6));
        dc.DrawLine(Caneta(Grafite, 3), new Point(3, 17), new Point(29, 17));
        dc.DrawLine(Caneta(Vermelho, 2), new Point(5, 10), new Point(27, 10));
        dc.DrawLine(Caneta(Vermelho, 2), new Point(5, 21), new Point(27, 21));
    }

    public static void Diametros(DrawingContext dc)
    {
        // Tubo que afina a cada derivação, como o ramal dimensionado.
        dc.DrawLine(Caneta(Grafite, 8), new Point(2, 16), new Point(12, 16));
        dc.DrawLine(Caneta(Vermelho, 5), new Point(12, 16), new Point(21, 16));
        dc.DrawLine(Caneta(Vermelho, 2.5), new Point(21, 16), new Point(30, 16));
        dc.DrawLine(Caneta(Grafite, 1.4), new Point(12, 8), new Point(12, 24));
        dc.DrawLine(Caneta(Grafite, 1.4), new Point(21, 8), new Point(21, 24));
    }

    public static void LigarBicos(DrawingContext dc)
    {
        dc.DrawLine(Caneta(Grafite, 3), new Point(3, 9), new Point(29, 9));
        var braco = Caneta(Vermelho, 2.2);
        dc.DrawLine(braco, new Point(10, 9), new Point(10, 22));
        dc.DrawGeometry(null, braco, Geometry.Parse("M 21,9 L 21,15 L 25,15 L 25,22"));
        dc.DrawEllipse(Vermelho, null, new Point(10, 25), 3, 3);
        dc.DrawEllipse(Vermelho, null, new Point(25, 25), 3, 3);
    }

    public static void Setor(DrawingContext dc)
    {
        // A descida da VGA com a bandeira do nome do setor.
        dc.DrawLine(Caneta(Grafite, 3), new Point(9, 29), new Point(9, 12));
        dc.DrawLine(Caneta(Grafite, 3), new Point(9, 12), new Point(28, 12));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 13,3 L 27,6.5 L 13,10 Z"));
        dc.DrawLine(Caneta(Grafite, 1.6), new Point(13, 2), new Point(13, 12));
        dc.DrawEllipse(Vermelho, null, new Point(9, 27), 3.2, 3.2);
    }

    public static void Verificar(DrawingContext dc)
    {
        dc.DrawEllipse(Brushes.White, Caneta(Grafite, 2.5), new Point(16, 16), 12, 12);
        dc.DrawGeometry(null, Caneta(Vermelho, 3.5), Geometry.Parse("M 9.5,16.5 L 14,21 L 23,11"));
    }

    public static void Livro(DrawingContext dc)
    {
        var caneta = Caneta(Grafite, 2.2);
        dc.DrawGeometry(Brushes.White, caneta, Geometry.Parse("M 16,8 C 12,5 7,5 3,6 L 3,26 C 7,25 12,25 16,28 Z"));
        dc.DrawGeometry(Brushes.White, caneta, Geometry.Parse("M 16,8 C 20,5 25,5 29,6 L 29,26 C 25,25 20,25 16,28 Z"));
        var linha = Caneta(Vermelho, 1.8);
        foreach (var y in new[] { 12.0, 16.5, 21.0 })
        {
            dc.DrawLine(linha, new Point(6.5, y - 1), new Point(12.5, y));
            dc.DrawLine(linha, new Point(19.5, y), new Point(25.5, y - 1));
        }
    }

    public static void Regua(DrawingContext dc)
    {
        // Régua inclinada: o padrão de alturas e medidas do escritório.
        dc.PushTransform(new RotateTransform(-35, 16, 16));
        dc.DrawRectangle(Brushes.White, Caneta(Grafite, 2.2), new System.Windows.Rect(4, 11, 24, 10));
        var traco = Caneta(Vermelho, 1.6);
        foreach (var x in new[] { 8.0, 12.0, 16.0, 20.0, 24.0 })
            dc.DrawLine(traco, new Point(x, 11), new Point(x, x % 8 == 0 ? 17 : 14.5));
        dc.Pop();
    }

    public static void Familias(DrawingContext dc)
    {
        // Três símbolos de planta: tomada, luminária e interruptor.
        dc.DrawEllipse(Brushes.White, Caneta(Grafite, 2.2), new Point(9, 10), 5, 5);
        dc.DrawLine(Caneta(Vermelho, 2), new Point(9, 5), new Point(9, 15));
        dc.DrawEllipse(Brushes.White, Caneta(Grafite, 2.2), new Point(23, 10), 5, 5);
        dc.DrawLine(Caneta(Vermelho, 2), new Point(19.5, 6.5), new Point(26.5, 13.5));
        dc.DrawLine(Caneta(Vermelho, 2), new Point(26.5, 6.5), new Point(19.5, 13.5));
        dc.DrawEllipse(Vermelho, null, new Point(16, 24), 2.5, 2.5);
        dc.DrawLine(Caneta(Grafite, 2.2), new Point(16, 24), new Point(23, 19));
    }

    public static void Quadro(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, Caneta(Grafite, 2.4), new System.Windows.Rect(6, 4, 20, 24));
        var disjuntor = Caneta(Vermelho, 2.4);
        foreach (var y in new[] { 10.0, 16.0, 22.0 })
        {
            dc.DrawLine(disjuntor, new Point(10, y), new Point(15, y));
            dc.DrawLine(disjuntor, new Point(17, y), new Point(22, y));
        }
    }

    public static void Alimentador(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, Caneta(Grafite, 2.2), new System.Windows.Rect(3, 4, 10, 12));
        dc.DrawRectangle(Brushes.White, Caneta(Grafite, 2.2), new System.Windows.Rect(19, 16, 10, 12));
        dc.DrawGeometry(null, Caneta(Vermelho, 2.6), Geometry.Parse("M 13,10 L 16,10 L 16,22 L 19,22"));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 17,19 L 21,22 L 17,25 Z"));
    }

    public static void Tomada(DrawingContext dc)
    {
        dc.DrawEllipse(Brushes.White, Caneta(Grafite, 2.6), new Point(16, 16), 11, 11);
        dc.DrawEllipse(Vermelho, null, new Point(11.5, 14), 2.2, 2.2);
        dc.DrawEllipse(Vermelho, null, new Point(20.5, 14), 2.2, 2.2);
        dc.DrawEllipse(Vermelho, null, new Point(16, 21), 2.2, 2.2);
    }

    public static void Interruptor(DrawingContext dc)
    {
        // Interruptor com o fio até a lâmpada.
        dc.DrawEllipse(Brushes.White, Caneta(Grafite, 2.4), new Point(8, 24), 4, 4);
        dc.DrawLine(Caneta(Vermelho, 2.6), new Point(8, 20), new Point(14, 10));
        dc.DrawLine(Caneta(Grafite, 2), new Point(14, 10), new Point(24, 10));
        dc.DrawEllipse(Brushes.White, Caneta(Grafite, 2.4), new Point(24, 7), 4, 4);
        dc.DrawLine(Caneta(Vermelho, 1.8), new Point(21.5, 4.5), new Point(26.5, 9.5));
        dc.DrawLine(Caneta(Vermelho, 1.8), new Point(26.5, 4.5), new Point(21.5, 9.5));
    }

    public static void Circuito(DrawingContext dc)
    {
        // Barramento do quadro com três circuitos saindo.
        dc.DrawLine(Caneta(Grafite, 3.2), new Point(6, 6), new Point(6, 26));
        var saida = Caneta(Vermelho, 2.4);
        foreach (var y in new[] { 9.0, 16.0, 23.0 })
        {
            dc.DrawLine(saida, new Point(6, y), new Point(22, y));
            dc.DrawLine(Caneta(Grafite, 2), new Point(13, y - 3), new Point(17, y));
            dc.DrawEllipse(Vermelho, null, new Point(25, y), 2.2, 2.2);
        }
    }

    static Pen Caneta(Brush cor, double espessura) =>
        Congelado(new Pen(cor, espessura) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round });

    static T Congelado<T>(T objeto) where T : Freezable
    {
        objeto.Freeze();
        return objeto;
    }
}
