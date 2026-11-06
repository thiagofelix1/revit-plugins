using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using FormattedText = System.Windows.Media.FormattedText;
using FlowDirection = System.Windows.FlowDirection;

namespace Eletrica.AddIn;

/// <summary>
///     Ícones da faixa de opções, um por botão, desenhados num quadro de 32×32 e rasterizados no tamanho pedido.
///     Grafite no contorno, vermelho no que o botão faz; as cores dos condutores (fase, neutro, PE) e o raio amarelo só
///     onde há fio ou energia.
/// </summary>
static class Icones
{
    static readonly Brush Vermelho = Congelado(new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)));
    static readonly Brush Grafite = Congelado(new SolidColorBrush(Color.FromRgb(0x37, 0x47, 0x4F)));
    static readonly Brush Cinza = Congelado(new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE)));
    static readonly Brush Claro = Congelado(new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF1)));
    static readonly Brush Amarelo = Congelado(new SolidColorBrush(Color.FromRgb(0xF9, 0xA8, 0x25)));
    static readonly Brush Azul = Congelado(new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5)));
    static readonly Brush Verde = Congelado(new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47)));
    static readonly Brush Branco = Brushes.White;

    /// <summary>
    ///     Rasteriza o desenho num bitmap de <paramref name="tamanho" /> unidades (o que o Revit pede) com o dobro de pixels,
    ///     para ficar nítido em tela de 150–200 %. O desenho é feito em unidades de tela: a escala é tamanho/32, não pixels/32
    ///     (com pixels/32 o desenho saía com o dobro do tamanho e só o canto de cima à esquerda aparecia no botão).
    /// </summary>
    public static ImageSource Gerar(Action<DrawingContext> desenhar, int tamanho)
    {
        const double escala = 2;
        const double margem = 1;
        var pixels = (int)(tamanho * escala);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(tamanho / 32.0, tamanho / 32.0));
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

    // ---------------------------------------------------------------- Projeto

    /// <summary>Prancheta com os dados e o raio: tensões, fases, aterramento.</summary>
    public static void DadosDoProjeto(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(5, 4, 19, 25), 2, 2);
        dc.DrawRoundedRectangle(Grafite, null, new Rect(10, 2, 9, 5), 1.5, 1.5);
        var linha = Caneta(Grafite, 1.8);
        foreach (var y in new[] { 12.0, 17.0, 22.0 })
        {
            dc.DrawEllipse(Vermelho, null, new Point(9.5, y), 1.4, 1.4);
            dc.DrawLine(linha, new Point(12.5, y), new Point(18, y));
        }
        Raio(dc, 19.5, 13, 1.0);
    }

    /// <summary>Planta com três cômodos, cada um com a etiqueta do uso.</summary>
    public static void Ambientes(DrawingContext dc)
    {
        var parede = Caneta(Grafite, 2.4);
        dc.DrawRectangle(Branco, parede, new Rect(3, 4, 26, 24));
        dc.DrawLine(parede, new Point(16, 4), new Point(16, 12));
        dc.DrawLine(parede, new Point(16, 17), new Point(16, 28));
        dc.DrawLine(parede, new Point(3, 18), new Point(9, 18));
        dc.DrawLine(parede, new Point(13, 18), new Point(16, 18));
        foreach (var (x, y) in new[] { (5.5, 8.5), (19, 8.5), (5.5, 21.5) })
            dc.DrawRoundedRectangle(Vermelho, null, new Rect(x, y, 7.5, 3.6), 1, 1);
        dc.DrawLine(Caneta(Cinza, 1.4), new Point(19, 23), new Point(26, 23));
        dc.DrawLine(Caneta(Cinza, 1.4), new Point(19, 19.5), new Point(24, 19.5));
    }

    /// <summary>Parede em vista com a régua e os pontos nas alturas do padrão (baixa, média, alta).</summary>
    public static void Padrao(DrawingContext dc)
    {
        dc.DrawLine(Caneta(Grafite, 3), new Point(2, 29), new Point(30, 29));
        dc.DrawRectangle(Branco, Caneta(Grafite, 1.8), new Rect(3, 3, 6, 26));
        var traco = Caneta(Grafite, 1.2);
        for (var y = 6.0; y < 28; y += 3) dc.DrawLine(traco, new Point(3, y), new Point(y % 6 == 0 ? 7 : 5.5, y));
        var cota = Caneta(Cinza, 1.3) ;
        cota.DashStyle = new DashStyle([1.5, 1.5], 0);
        foreach (var (y, w) in new[] { (24.0, 7.0), (15.0, 7.0), (6.5, 7.0) })
        {
            dc.DrawLine(cota, new Point(9.5, y), new Point(20, y));
            dc.DrawRoundedRectangle(Branco, Caneta(Vermelho, 2), new Rect(20.5, y - 3, w, 6), 1.2, 1.2);
            dc.DrawEllipse(Vermelho, null, new Point(24, y), 1.3, 1.3);
        }
    }

    /// <summary>Os símbolos de planta que o plugin reconhece pelo papel: tomada, luminária, interruptor e quadro.</summary>
    public static void Familias(DrawingContext dc)
    {
        foreach (var (x, y) in new[] { (2.0, 2.0), (17.0, 2.0), (2.0, 17.0), (17.0, 17.0) })
            dc.DrawRoundedRectangle(Claro, null, new Rect(x, y, 13, 13), 2.5, 2.5);
        // tomada: triângulo meio cheio
        dc.DrawGeometry(Branco, Caneta(Grafite, 1.8), Geometry.Parse("M 4.5,12.5 L 12.5,12.5 L 8.5,4.5 Z"));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 4.5,12.5 L 8.5,12.5 L 8.5,4.5 Z"));
        // luminária: círculo com X
        dc.DrawEllipse(Branco, Caneta(Grafite, 1.8), new Point(23.5, 8.5), 4.5, 4.5);
        dc.DrawLine(Caneta(Vermelho, 1.6), new Point(20.3, 5.3), new Point(26.7, 11.7));
        dc.DrawLine(Caneta(Vermelho, 1.6), new Point(26.7, 5.3), new Point(20.3, 11.7));
        // interruptor: ponto com a alavanca
        dc.DrawEllipse(Vermelho, null, new Point(6.5, 26.5), 2.4, 2.4);
        dc.DrawLine(Caneta(Grafite, 2), new Point(6.5, 26.5), new Point(12, 20.5));
        // quadro: retângulo com a diagonal cheia
        dc.DrawRectangle(Branco, Caneta(Grafite, 1.8), new Rect(19, 20, 9, 7));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 19,27 L 28,20 L 28,27 Z"));
    }

    /// <summary>Etiqueta com "AE": os parâmetros compartilhados do plugin.</summary>
    public static void Parametros(DrawingContext dc)
    {
        dc.DrawGeometry(Claro, Caneta(Cinza, 1.6), Geometry.Parse("M 6,4 L 20,4 L 28,11 L 20,18 L 6,18 Z"));
        dc.DrawGeometry(Branco, Caneta(Grafite, 2.2), Geometry.Parse("M 3,11 L 18,11 L 27,19.5 L 18,28 L 3,28 Z"));
        dc.DrawEllipse(Branco, Caneta(Grafite, 1.6), new Point(20.5, 19.5), 1.8, 1.8);
        Texto(dc, "AE", 10.5, Vermelho, new Point(4.5, 13.2), negrito: true);
    }

    // ---------------------------------------------------------------- Lançamento

    /// <summary>Quadro de distribuição com duas fileiras de disjuntores.</summary>
    public static void Quadro(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.4), new Rect(5, 2, 22, 28), 2, 2);
        dc.DrawRectangle(Grafite, null, new Rect(5, 2, 22, 5));
        foreach (var x in new[] { 9.0, 18.0 })
            foreach (var y in new[] { 10.0, 15.5, 21.0 })
            {
                dc.DrawRoundedRectangle(Claro, Caneta(Grafite, 1.2), new Rect(x, y, 5, 4), 0.8, 0.8);
                dc.DrawRectangle(Vermelho, null, new Rect(x + 1.5, y + 0.6, 2, 1.6));
            }
        dc.DrawLine(Caneta(Cinza, 1.4), new Point(9, 27), new Point(23, 27));
    }

    /// <summary>Quadro geral alimentando um quadro terminal.</summary>
    public static void AlimentarQuadro(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(2, 2, 12, 16), 1.5, 1.5);
        dc.DrawRectangle(Grafite, null, new Rect(2, 2, 12, 3.5));
        foreach (var y in new[] { 8.0, 12.0 }) dc.DrawRectangle(Vermelho, null, new Rect(5, y, 6, 2));
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(20, 17, 10, 13), 1.5, 1.5);
        dc.DrawRectangle(Grafite, null, new Rect(20, 17, 10, 3));
        dc.DrawRectangle(Vermelho, null, new Rect(22.5, 23, 5, 2));
        dc.DrawGeometry(null, Caneta(Vermelho, 2.6), Geometry.Parse("M 14,10 L 25,10 L 25,13"));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 21.5,12 L 28.5,12 L 25,16.5 Z"));
    }

    /// <summary>Tomada de três pinos (NBR 14136) com o sinal de acrescentar.</summary>
    public static void InserirPonto(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(2, 3, 22, 22), 4, 4);
        dc.DrawEllipse(Claro, Caneta(Grafite, 1.6), new Point(13, 14), 7.5, 7.5);
        dc.DrawEllipse(Grafite, null, new Point(10, 12.5), 1.6, 1.6);
        dc.DrawEllipse(Grafite, null, new Point(16, 12.5), 1.6, 1.6);
        dc.DrawEllipse(Grafite, null, new Point(13, 17.5), 1.6, 1.6);
        dc.DrawEllipse(Vermelho, Caneta(Branco, 1.5), new Point(24, 24), 6.5, 6.5);
        var mais = Caneta(Branco, 2.2);
        dc.DrawLine(mais, new Point(24, 20.5), new Point(24, 27.5));
        dc.DrawLine(mais, new Point(20.5, 24), new Point(27.5, 24));
    }

    /// <summary>Planta de um cômodo com tomadas pelas paredes, a luz no centro e o interruptor na porta.</summary>
    public static void PontosPorAmbiente(DrawingContext dc)
    {
        var parede = Caneta(Grafite, 2.4);
        dc.DrawGeometry(null, parede, Geometry.Parse("M 11,29 L 3,29 L 3,3 L 29,3 L 29,29 L 19,29"));
        dc.DrawGeometry(null, Caneta(Cinza, 1.2), Geometry.Parse("M 19,29 A 8,8 0 0 0 11,21"));
        foreach (var (pt, rot) in new[] { (new Point(6, 11), 90.0), (new Point(6, 22), 90.0), (new Point(16, 6), 180.0), (new Point(26, 12), 270.0), (new Point(26, 22), 270.0) })
        {
            dc.PushTransform(new RotateTransform(rot, pt.X, pt.Y));
            dc.DrawGeometry(Vermelho, null, Geometry.Parse($"M {F(pt.X - 2.8)},{F(pt.Y + 2)} L {F(pt.X + 2.8)},{F(pt.Y + 2)} L {F(pt.X)},{F(pt.Y - 2.6)} Z"));
            dc.Pop();
        }
        dc.DrawEllipse(Branco, Caneta(Grafite, 1.6), new Point(16, 15), 3.6, 3.6);
        dc.DrawLine(Caneta(Vermelho, 1.4), new Point(13.5, 12.5), new Point(18.5, 17.5));
        dc.DrawLine(Caneta(Vermelho, 1.4), new Point(18.5, 12.5), new Point(13.5, 17.5));
        dc.DrawEllipse(Grafite, null, new Point(8.5, 26), 1.6, 1.6);
    }

    /// <summary>Interruptor ligado à lâmpada que ele acende.</summary>
    public static void Comando(DrawingContext dc)
    {
        dc.DrawGeometry(null, Caneta(Cinza, 1.6), Geometry.Parse("M 8,17 C 8,8 14,22 20,20"));
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(2, 17, 12, 13), 2, 2);
        dc.DrawRoundedRectangle(Vermelho, null, new Rect(5.5, 19.5, 5, 4.5), 1, 1);
        dc.DrawRoundedRectangle(Claro, null, new Rect(5.5, 24, 5, 3.5), 1, 1);
        // lâmpada
        dc.DrawGeometry(Branco, Caneta(Grafite, 2), Geometry.Parse("M 18.5,19 C 18.5,15 14.5,13.5 14.5,9.5 A 7,7 0 1 1 28.5,9.5 C 28.5,13.5 24.5,15 24.5,19 Z"));
        dc.DrawRectangle(Grafite, null, new Rect(18.5, 20, 6, 2));
        dc.DrawRectangle(Grafite, null, new Rect(19.5, 23, 4, 1.6));
        dc.DrawGeometry(null, Caneta(Amarelo, 1.8), Geometry.Parse("M 19,13 L 20.5,9.5 L 22.5,13 L 24,9.5"));
    }

    /// <summary>Quadro com três circuitos saindo, cada um com uma cor.</summary>
    public static void CriarCircuitos(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(2, 5, 10, 22), 1.5, 1.5);
        dc.DrawRectangle(Grafite, null, new Rect(2, 5, 10, 3.5));
        var cores = new[] { Vermelho, Amarelo, Azul };
        var ys = new[] { 11.5, 16.5, 21.5 };
        var destinos = new[] { 6.0, 16.5, 27.0 };
        for (var k = 0; k < 3; k++)
        {
            dc.DrawRectangle(cores[k], null, new Rect(4.5, ys[k] - 1, 5, 2));
            dc.DrawGeometry(null, Caneta(cores[k], 2.2), Geometry.Parse($"M 12,{F(ys[k])} C 18,{F(ys[k])} 18,{F(destinos[k])} 24,{F(destinos[k])}"));
            dc.DrawEllipse(cores[k], Caneta(Grafite, 1.2), new Point(26.5, destinos[k]), 3, 3);
        }
    }

    // ---------------------------------------------------------------- Eletrodutos

    /// <summary>Eletroduto em L sendo desenhado: o lápis na ponta.</summary>
    public static void TracarEletroduto(DrawingContext dc)
    {
        Tubo(dc, "M 3,25 L 13,25 L 13,9 L 20,9", 6);
        dc.DrawEllipse(Grafite, null, new Point(3, 25), 2.6, 2.6);
        dc.PushTransform(new RotateTransform(45, 24, 9));
        dc.DrawRectangle(Vermelho, Caneta(Grafite, 1), new Rect(21.5, -1, 5, 12));
        dc.DrawRectangle(Claro, Caneta(Grafite, 1), new Rect(21.5, -3, 5, 2));
        dc.DrawGeometry(Branco, Caneta(Grafite, 1), Geometry.Parse("M 21.5,11 L 26.5,11 L 24,15.5 Z"));
        dc.DrawGeometry(Grafite, null, Geometry.Parse("M 23.2,14 L 24.8,14 L 24,15.5 Z"));
        dc.Pop();
    }

    /// <summary>Eletroduto do teto com as descidas até os pontos.</summary>
    public static void LigarPontos(DrawingContext dc)
    {
        Tubo(dc, "M 2,7 L 30,7", 6);
        Tubo(dc, "M 9,7 L 9,22", 4.4);
        Tubo(dc, "M 23,7 L 23,22", 4.4);
        foreach (var x in new[] { 9.0, 23.0 })
        {
            dc.DrawRoundedRectangle(Vermelho, Caneta(Grafite, 1.4), new Rect(x - 4, 22, 8, 7), 1.2, 1.2);
            dc.DrawEllipse(Branco, null, new Point(x, 25.5), 1.4, 1.4);
        }
    }

    /// <summary>Caixa de passagem no meio do eletroduto, com a cota de 15 m entre caixas.</summary>
    public static void CaixasPorRegra(DrawingContext dc)
    {
        Tubo(dc, "M 2,22 L 30,22", 5.5);
        dc.DrawRectangle(Branco, Caneta(Vermelho, 2.4), new Rect(11, 16.5, 10, 11));
        dc.DrawLine(Caneta(Vermelho, 1.5), new Point(11, 16.5), new Point(21, 27.5));
        dc.DrawLine(Caneta(Vermelho, 1.5), new Point(21, 16.5), new Point(11, 27.5));
        var cota = Caneta(Grafite, 1.4);
        dc.DrawLine(cota, new Point(3, 10), new Point(16, 10));
        dc.DrawLine(cota, new Point(3, 7), new Point(3, 13));
        dc.DrawLine(cota, new Point(16, 7), new Point(16, 13));
        Texto(dc, "15", 7.5, Grafite, new Point(17.5, 5.2), negrito: true);
    }

    // ---------------------------------------------------------------- Fiação

    /// <summary>Corte do eletroduto com fase, neutro e PE passando por dentro.</summary>
    public static void PassarCircuitos(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Claro, Caneta(Grafite, 2.2), new Rect(2, 8, 18, 16), 3, 3);
        var fios = new[] { (Vermelho, 12.0, 6.0), (Azul, 16.0, 16.0), (Verde, 20.0, 26.0) };
        foreach (var (cor, y, destino) in fios)
        {
            dc.DrawLine(Caneta(cor, 2.4), new Point(4, y), new Point(20, y));
            dc.DrawGeometry(null, Caneta(cor, 2.4), Geometry.Parse($"M 20,{F(y)} C 25,{F(y)} 24,{F(destino)} 29,{F(destino)}"));
        }
        dc.DrawLine(Caneta(Grafite, 2.2), new Point(20, 8), new Point(20, 24));
    }

    /// <summary>O cursor escolhendo por onde o circuito passa: um caminho forçado, outro evitado.</summary>
    public static void FiacaoManual(DrawingContext dc)
    {
        Tubo(dc, "M 2,7 L 30,7", 5);
        dc.DrawLine(Caneta(Vermelho, 1.8), new Point(3, 7), new Point(29, 7));
        Tubo(dc, "M 2,17 L 14,17", 5);
        dc.DrawEllipse(null, Caneta(Cinza, 1.6), new Point(8, 17), 4.2, 4.2);
        dc.DrawLine(Caneta(Cinza, 1.6), new Point(5, 20), new Point(11, 14));
        dc.DrawGeometry(Branco, Caneta(Grafite, 1.6), Geometry.Parse("M 17,9 L 17,27 L 21,23.5 L 24,30 L 27,28.5 L 24,22.5 L 29,22.5 Z"));
    }

    /// <summary>Fio em arco na planta com as marcas de fase, neutro, retorno e PE.</summary>
    public static void DesenharFiacao(DrawingContext dc)
    {
        dc.DrawGeometry(null, Caneta(Grafite, 2.2), Geometry.Parse("M 3,26 Q 16,-2 29,26"));
        dc.DrawEllipse(Branco, Caneta(Grafite, 1.8), new Point(3, 26), 2.4, 2.4);
        dc.DrawEllipse(Vermelho, Caneta(Grafite, 1.4), new Point(29, 26), 2.6, 2.6);
        // marcas perpendiculares ao arco perto do topo
        var marcas = new[] { (Vermelho, 10.5), (Azul, 14.0), (Amarelo, 17.5), (Verde, 21.0) };
        foreach (var (cor, x) in marcas)
        {
            var t = (x - 3) / 26.0;
            var y = (1 - t) * (1 - t) * 26 + 2 * (1 - t) * t * -2 + t * t * 26;
            dc.DrawLine(Caneta(cor, 2.2), new Point(x - 1.6, y - 3.4), new Point(x + 1.6, y + 3.4));
        }
    }

    /// <summary>Etiqueta de fiação com chamada até o eletroduto.</summary>
    public static void Etiquetar(DrawingContext dc)
    {
        Tubo(dc, "M 2,27 L 22,27", 4.6);
        dc.DrawLine(Caneta(Grafite, 1.4), new Point(11, 27), new Point(16, 16));
        dc.DrawEllipse(Grafite, null, new Point(11, 27), 1.6, 1.6);
        dc.DrawRoundedRectangle(Branco, Caneta(Vermelho, 2), new Rect(12, 4, 18, 12), 2, 2);
        dc.DrawLine(Caneta(Vermelho, 1.8), new Point(15.5, 8), new Point(26.5, 8));
        dc.DrawLine(Caneta(Grafite, 1.6), new Point(15.5, 12), new Point(23, 12));
    }

    /// <summary>Tabela de legenda: o símbolo de cada circuito e o texto.</summary>
    public static void Legenda(DrawingContext dc)
    {
        dc.DrawRectangle(Branco, Caneta(Grafite, 2.2), new Rect(3, 4, 26, 24));
        var linha = Caneta(Cinza, 1.2);
        foreach (var y in new[] { 12.0, 20.0 }) dc.DrawLine(linha, new Point(3, y), new Point(29, y));
        dc.DrawLine(linha, new Point(12, 4), new Point(12, 28));
        dc.DrawLine(Caneta(Vermelho, 2), new Point(5, 8), new Point(10, 8));
        dc.DrawLine(Caneta(Vermelho, 1.4), new Point(7, 6), new Point(8, 10));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 5,18.5 L 10,18.5 L 7.5,13.5 Z"));
        dc.DrawEllipse(null, Caneta(Vermelho, 1.6), new Point(7.5, 24), 2.4, 2.4);
        var texto = Caneta(Grafite, 1.6);
        foreach (var y in new[] { 8.0, 16.0, 24.0 }) dc.DrawLine(texto, new Point(15, y), new Point(26, y));
    }

    // ---------------------------------------------------------------- Cálculo

    /// <summary>Lupa com o visto: o que a NBR 5410 cobra, conferido no modelo.</summary>
    public static void Verificar(DrawingContext dc)
    {
        dc.DrawLine(Caneta(Grafite, 5), new Point(20, 20), new Point(28.5, 28.5));
        dc.DrawEllipse(Branco, Caneta(Grafite, 2.6), new Point(13, 13), 10, 10);
        dc.DrawGeometry(null, Caneta(Vermelho, 3), Geometry.Parse("M 8,13.5 L 11.5,17 L 18.5,9"));
    }

    /// <summary>Calculadora com o raio no visor.</summary>
    public static void Calcular(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(5, 2, 22, 28), 2.5, 2.5);
        dc.DrawRoundedRectangle(Grafite, null, new Rect(8, 5, 16, 7), 1, 1);
        Raio(dc, 16, 5.6, 0.36, contorno: false);
        for (var linha = 0; linha < 3; linha++)
            for (var coluna = 0; coluna < 3; coluna++)
            {
                var cor = linha == 2 && coluna == 2 ? Vermelho : Claro;
                dc.DrawRoundedRectangle(cor, Caneta(Grafite, 0.9), new Rect(8.5 + coluna * 5.3, 14.5 + linha * 4.8, 4, 3.4), 0.7, 0.7);
            }
    }

    // ---------------------------------------------------------------- Resultados

    /// <summary>Memorial: documento com o selo de conferido.</summary>
    public static void Memorial(DrawingContext dc)
    {
        dc.DrawGeometry(Branco, Caneta(Grafite, 2.2), Geometry.Parse("M 4,2 L 18,2 L 25,9 L 25,29 L 4,29 Z"));
        dc.DrawGeometry(Claro, Caneta(Grafite, 1.6), Geometry.Parse("M 18,2 L 18,9 L 25,9"));
        var linha = Caneta(Grafite, 1.6);
        foreach (var y in new[] { 13.0, 17.0, 21.0 }) dc.DrawLine(linha, new Point(8, y), new Point(19, y));
        dc.DrawEllipse(Vermelho, Caneta(Branco, 1.5), new Point(24, 24), 6, 6);
        dc.DrawGeometry(null, Caneta(Branco, 2), Geometry.Parse("M 21,24 L 23.2,26.2 L 27,22"));
    }

    /// <summary>Manual do usuário: livreto com a casa na capa.</summary>
    public static void ManualDoUsuario(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Grafite, null, new Rect(6, 3, 21, 27), 2, 2);
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2), new Rect(4, 2, 21, 26), 2, 2);
        dc.DrawLine(Caneta(Grafite, 2), new Point(8, 2), new Point(8, 28));
        dc.DrawGeometry(Vermelho, null, Geometry.Parse("M 10.5,13 L 16.5,7.5 L 22.5,13 Z"));
        dc.DrawRectangle(Branco, Caneta(Vermelho, 1.8), new Rect(12, 13, 9, 7));
        dc.DrawRectangle(Vermelho, null, new Rect(15.2, 16, 2.6, 4));
        dc.DrawLine(Caneta(Cinza, 1.4), new Point(11, 24), new Point(22, 24));
    }

    /// <summary>Diagrama unifilar: entrada, barramento e circuitos com disjuntor.</summary>
    public static void Quadros(DrawingContext dc)
    {
        var condutor = Caneta(Grafite, 2);
        dc.DrawLine(condutor, new Point(16, 1.5), new Point(16, 8));
        dc.DrawLine(Caneta(Grafite, 3.2), new Point(4, 8), new Point(28, 8));
        foreach (var x in new[] { 7.0, 16.0, 25.0 })
        {
            dc.DrawLine(condutor, new Point(x, 8), new Point(x, 13));
            dc.DrawLine(Caneta(Vermelho, 2.2), new Point(x, 13), new Point(x + 3.5, 18));
            dc.DrawEllipse(Vermelho, null, new Point(x, 13), 1.3, 1.3);
            dc.DrawLine(condutor, new Point(x, 19), new Point(x, 26));
            dc.DrawGeometry(Grafite, null, Geometry.Parse($"M {F(x - 2.6)},25 L {F(x + 2.6)},25 L {F(x)},29.5 Z"));
        }
    }

    /// <summary>Lista de material: prancheta com itens conferidos.</summary>
    public static void ListaDeMaterial(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 2.2), new Rect(5, 4, 22, 26), 2, 2);
        dc.DrawRoundedRectangle(Grafite, null, new Rect(11, 2, 10, 5), 1.5, 1.5);
        foreach (var y in new[] { 11.0, 17.0, 23.0 })
        {
            dc.DrawRoundedRectangle(Branco, Caneta(Grafite, 1.3), new Rect(8.5, y - 2, 4, 4), 0.6, 0.6);
            dc.DrawGeometry(null, Caneta(Vermelho, 1.8), Geometry.Parse($"M 9,{F(y)} L 10.5,{F(y + 1.5)} L 13.5,{F(y - 2.5)}"));
            dc.DrawLine(Caneta(Grafite, 1.6), new Point(15.5, y), new Point(23.5, y));
        }
    }

    /// <summary>Prancha com a planta e o carimbo.</summary>
    public static void Pranchas(DrawingContext dc)
    {
        dc.DrawRectangle(Cinza, null, new Rect(3, 6.5, 27, 21));
        dc.DrawRectangle(Branco, Caneta(Grafite, 2), new Rect(1.5, 5, 27, 21));
        var planta = Caneta(Cinza, 1.4);
        dc.DrawRectangle(null, planta, new Rect(5, 8, 13, 13));
        dc.DrawLine(planta, new Point(11, 8), new Point(11, 15));
        dc.DrawLine(Caneta(Vermelho, 1.6), new Point(6.5, 18), new Point(16.5, 18));
        dc.DrawEllipse(Vermelho, null, new Point(14.5, 11.5), 1.4, 1.4);
        dc.DrawRectangle(Claro, Caneta(Vermelho, 1.6), new Rect(20, 18, 7, 6.5));
        dc.DrawLine(Caneta(Vermelho, 1.2), new Point(20, 21), new Point(27, 21));
    }

    // ---------------------------------------------------------------- Ajuda

    /// <summary>Guia: interrogação no círculo.</summary>
    public static void Guia(DrawingContext dc)
    {
        dc.DrawEllipse(Vermelho, null, new Point(16, 16), 14, 14);
        dc.DrawGeometry(null, Caneta(Branco, 3.4), Geometry.Parse("M 11.5,12 A 4.5,4.5 0 1 1 18.5,15.6 C 16.8,16.8 16,17.6 16,20"));
        dc.DrawEllipse(Branco, null, new Point(16, 24.6), 2, 2);
    }

    // ---------------------------------------------------------------- peças

    /// <summary>Eletroduto: traço grafite largo com o miolo claro, para ler como tubo em qualquer escala.</summary>
    static void Tubo(DrawingContext dc, string caminho, double largura)
    {
        var geometria = Geometry.Parse(caminho);
        dc.DrawGeometry(null, Caneta(Grafite, largura, PenLineCap.Flat), geometria);
        dc.DrawGeometry(null, Caneta(Claro, largura * 0.45, PenLineCap.Flat), geometria);
    }

    /// <summary>Raio amarelo com o canto de cima à esquerda em (x, y), no tamanho 1 = 10×16.</summary>
    static void Raio(DrawingContext dc, double x, double y, double tamanho, bool contorno = true)
    {
        dc.PushTransform(new TranslateTransform(x, y));
        dc.PushTransform(new ScaleTransform(tamanho, tamanho));
        dc.DrawGeometry(Amarelo, contorno ? Caneta(Grafite, 1.3) : null, Geometry.Parse("M 6,0 L 0,9 L 4.5,9 L 2.5,16 L 10,6 L 5.5,6 L 8,0 Z"));
        dc.Pop();
        dc.Pop();
    }

    static void Texto(DrawingContext dc, string texto, double tamanho, Brush cor, Point origem, bool negrito = false)
    {
        var fonte = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, negrito ? FontWeights.Bold : FontWeights.SemiBold, FontStretches.Normal);
        var formatado = new FormattedText(texto, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, fonte, tamanho, cor, 1.0);
        dc.DrawText(formatado, origem);
    }

    static string F(double valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

    static Pen Caneta(Brush cor, double espessura, PenLineCap ponta = PenLineCap.Round)
    {
        var caneta = new Pen(cor, espessura) { StartLineCap = ponta, EndLineCap = ponta, LineJoin = PenLineJoin.Round };
        return caneta;
    }

    static T Congelado<T>(T objeto) where T : Freezable
    {
        objeto.Freeze();
        return objeto;
    }
}
