using System.Globalization;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Entidades;
using Cysharp.Text;
using Spectre.Console;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Dibujado de la lista de conversaciones: la pantalla principal del cliente.
/// </summary>
/// <remarks>
/// Cada conversación ocupa una línea con la misma información que uno espera de una
/// aplicación de mensajería: con quién es, qué fue lo último que se dijo, cuándo y
/// cuánto queda por leer. El formato se compone a mano en lugar de con una tabla
/// porque la fila seleccionada tiene que poder resaltarse entera.
/// </remarks>
public static class PresentacionChats
{
    /// <summary>Anchura reservada al nombre de la conversación.</summary>
    private const int AnchoNombre = 22;

    /// <summary>Anchura reservada a la marca de tiempo.</summary>
    private const int AnchoTiempo = 8;

    /// <summary>Dibuja la cabecera de la pantalla de conversaciones.</summary>
    /// <param name="nombreUsuario">Usuario conectado.</param>
    /// <param name="servidor">Servidor al que apunta el cliente.</param>
    /// <param name="estadoConexion">Estado de la conexión en tiempo real.</param>
    /// <param name="pendientes">Total de mensajes sin leer.</param>
    public static void Cabecera(string nombreUsuario, string servidor, string estadoConexion, int pendientes)
    {
        var resumen = pendientes == 0
            ? "[grey]sin mensajes pendientes[/]"
            : $"[bold red]● {pendientes}[/] [grey]sin leer[/]";

        var contenido = new Markup(
            $"[bold {Presentacion.ColorPrincipal}]dotChat[/]  [grey]·[/]  " +
            $"[{Presentacion.ColorDe(nombreUsuario)}]●[/] [bold]{Presentacion.Escapar(nombreUsuario)}[/]  [grey]·[/]  " +
            $"{EtiquetaEstado(estadoConexion)}  [grey]·[/]  {resumen}\n" +
            $"[grey]{Presentacion.Escapar(servidor)}[/]");

        AnsiConsole.Write(new Panel(contenido)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey37),
            Padding = new Padding(1, 0, 1, 0),
            Expand = true
        });
    }

    /// <summary>Dibuja la lista de conversaciones con la fila indicada resaltada.</summary>
    /// <param name="salas">Conversaciones ya ordenadas.</param>
    /// <param name="seleccionada">Índice de la fila resaltada.</param>
    public static void Lista(IReadOnlyList<SalaDto> salas, int seleccionada)
    {
        ArgumentNullException.ThrowIfNull(salas);

        if (salas.Count == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("  [grey]Todavía no tienes conversaciones.[/]");
            AnsiConsole.MarkupLine(
                $"  [grey]Pulsa[/] [bold {Presentacion.ColorPrincipal}]n[/] [grey]para escribir a alguien " +
                $"o[/] [bold {Presentacion.ColorPrincipal}]u[/] [grey]para ver quién hay.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        for (var indice = 0; indice < salas.Count; indice++)
        {
            AnsiConsole.MarkupLine(Fila(salas[indice], indice == seleccionada));
        }
    }

    /// <summary>Dibuja el pie con los atajos disponibles.</summary>
    public static void Atajos()
    {
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
        AnsiConsole.MarkupLine(
            "[grey]↑↓[/] moverse   [grey]intro[/] abrir   [grey]n[/] nuevo privado   " +
            "[grey]s[/] salas   [grey]c[/] crear sala   [grey]u[/] usuarios   " +
            "[grey]p[/] cuenta   [grey]r[/] refrescar   [grey]q[/] salir");
    }

    /// <summary>Compone la línea de una conversación.</summary>
    /// <param name="sala">Conversación a dibujar.</param>
    /// <param name="resaltada">Indica si es la fila seleccionada.</param>
    private static string Fila(SalaDto sala, bool resaltada)
    {
        using var constructor = ZString.CreateStringBuilder();

        // El cursor es un carácter y no un color de fondo: los fondos se comportan de
        // forma distinta en cada terminal y aquí lo que importa es que se vea siempre.
        constructor.Append(resaltada ? $"[{Presentacion.ColorPrincipal}]❯[/] " : "  ");

        constructor.Append(Indicador(sala));
        constructor.Append(' ');
        constructor.Append(NombreAjustado(sala, resaltada));
        constructor.Append("  ");
        constructor.Append(Previsualizacion(sala));

        var tiempo = sala.UltimoMensaje?.FechaEnvio ?? sala.FechaUltimaActividad;
        constructor.Append(" [grey]");
        constructor.Append(TiempoRelativo(tiempo).PadLeft(AnchoTiempo));
        constructor.Append("[/] ");

        constructor.Append(sala.MensajesSinLeer > 0
            ? $"[bold white on red] {sala.MensajesSinLeer} [/]"
            : "   ");

        return constructor.ToString();
    }

    /// <summary>Marca de la naturaleza de la conversación y de la presencia.</summary>
    /// <param name="sala">Conversación a marcar.</param>
    private static string Indicador(SalaDto sala) => sala.Tipo switch
    {
        // En una conversación directa el punto es la presencia del interlocutor; en
        // las salas, un símbolo que distingue las públicas de las privadas.
        TipoSala.Directa when sala.InterlocutorEnLinea == true => "[green]●[/]",
        TipoSala.Directa => "[grey]○[/]",
        TipoSala.Privada => "[yellow]▪[/]",
        _ => "[mediumpurple2]#[/]"
    };

    /// <summary>Recorta o rellena el nombre para que todas las filas cuadren.</summary>
    /// <param name="sala">Conversación.</param>
    /// <param name="resaltada">Indica si es la fila seleccionada.</param>
    private static string NombreAjustado(SalaDto sala, bool resaltada)
    {
        var nombre = sala.Nombre.Length > AnchoNombre
            ? string.Concat(sala.Nombre.AsSpan(0, AnchoNombre - 1), "…")
            : sala.Nombre.PadRight(AnchoNombre);

        var estilo = resaltada
            ? $"bold {Presentacion.ColorPrincipal}"
            : sala.MensajesSinLeer > 0 ? "bold white" : Presentacion.ColorDe(sala.Nombre);

        return $"[{estilo}]{Presentacion.Escapar(nombre)}[/]";
    }

    /// <summary>Compone la previsualización del último mensaje.</summary>
    /// <param name="sala">Conversación.</param>
    private static string Previsualizacion(SalaDto sala)
    {
        if (sala.UltimoMensaje is not { } ultimo)
        {
            return "[grey]sin mensajes[/]";
        }

        // En una conversación directa el nombre del otro ya está en la fila; solo se
        // marcan los mensajes propios, igual que hace cualquier cliente de mensajería.
        var autor = ultimo.EsPropio
            ? "[grey]Tú:[/] "
            : sala.EsDirecta ? string.Empty : $"[grey]{Presentacion.Escapar(ultimo.NombreAutor)}:[/] ";

        var cuerpo = ultimo switch
        {
            { TipoAdjunto: Chat.Dominio.Entidades.TipoAdjunto.Imagen } =>
                $"[mediumpurple2]🖼 {Presentacion.Escapar(ultimo.NombreAdjunto ?? "imagen")}[/]",
            { TipoAdjunto: Chat.Dominio.Entidades.TipoAdjunto.Archivo } =>
                $"[mediumpurple2]📎 {Presentacion.Escapar(ultimo.NombreAdjunto ?? "archivo")}[/]",
            _ => $"[grey]{Presentacion.Escapar(EnUnaLinea(ultimo.Texto))}[/]"
        };

        var pie = ultimo.TipoAdjunto is not null && ultimo.Texto.Length > 0
            ? $" [grey]{Presentacion.Escapar(EnUnaLinea(ultimo.Texto))}[/]"
            : string.Empty;

        return autor + cuerpo + pie;
    }

    /// <summary>Deja el texto en una sola línea para que no descuadre la lista.</summary>
    /// <param name="texto">Texto original.</param>
    private static string EnUnaLinea(string texto)
        => texto.Replace('\n', ' ').Replace('\r', ' ');

    /// <summary>
    /// Formatea una marca de tiempo como lo haría una aplicación de mensajería: la hora
    /// si es de hoy, «ayer» si es de ayer, y la fecha corta si es más antigua.
    /// </summary>
    /// <param name="fecha">Instante a formatear.</param>
    public static string TiempoRelativo(DateTimeOffset? fecha)
    {
        if (fecha is null)
        {
            return string.Empty;
        }

        var local = fecha.Value.ToLocalTime();
        var ahora = DateTimeOffset.Now;
        var transcurrido = ahora - local;

        if (transcurrido < TimeSpan.FromMinutes(1))
        {
            return "ahora";
        }

        if (local.Date == ahora.Date)
        {
            return local.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        if (local.Date == ahora.Date.AddDays(-1))
        {
            return "ayer";
        }

        return transcurrido < TimeSpan.FromDays(7)
            ? local.ToString("ddd", CultureInfo.CurrentCulture)
            : local.ToString("dd/MM", CultureInfo.InvariantCulture);
    }

    /// <summary>Etiqueta coloreada del estado de la conexión en tiempo real.</summary>
    /// <param name="estado">Estado comunicado por el cliente de SignalR.</param>
    private static string EtiquetaEstado(string estado) => estado switch
    {
        "conectado" or "reconectado" => "[green]● en línea[/]",
        "reconectando" => "[yellow]◐ reconectando[/]",
        "desconectado" => "[red]○ sin conexión[/]",
        _ => $"[grey]○ {Presentacion.Escapar(estado)}[/]"
    };
}
