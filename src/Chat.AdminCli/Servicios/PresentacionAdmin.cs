using System.Globalization;
using Chat.Aplicacion.Dtos;
using Chat.Dominio.Entidades;
using Spectre.Console;
using ZLinq;

namespace Chat.AdminCli.Servicios;

/// <summary>
/// Utilidades de presentación de la consola de administración.
/// Mantiene un estilo propio (tonos ámbar) para distinguirla del cliente de usuario.
/// </summary>
public static class PresentacionAdmin
{
    /// <summary>Color del borde de todas las tablas administrativas.</summary>
    private static readonly Color ColorBorde = Color.Orange1;

    /// <summary>
    /// Limpia la pantalla solo si hay un terminal real detrás. Con la salida
    /// redirigida (tuberías, ficheros de registro) la llamada fallaría.
    /// </summary>
    public static void LimpiarPantalla()
    {
        if (!Console.IsOutputRedirected)
        {
            AnsiConsole.Clear();
        }
    }

    /// <summary>Dibuja la cabecera de la consola administrativa.</summary>
    /// <param name="subtitulo">Texto secundario.</param>
    public static void Cabecera(string subtitulo)
    {
        AnsiConsole.Write(new Rule("[bold orange1]dotChat · administración[/]").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]{Escapar(subtitulo)}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>Muestra un mensaje de éxito.</summary>
    /// <param name="texto">Contenido del mensaje.</param>
    public static void Exito(string texto) => AnsiConsole.MarkupLine($"[green]✔[/] {Escapar(texto)}");

    /// <summary>Muestra un aviso.</summary>
    /// <param name="texto">Contenido del aviso.</param>
    public static void Aviso(string texto) => AnsiConsole.MarkupLine($"[yellow]![/] {Escapar(texto)}");

    /// <summary>Muestra un error dentro de un panel destacado.</summary>
    /// <param name="texto">Descripción del error.</param>
    public static void Error(string texto)
    {
        AnsiConsole.Write(new Panel(new Markup($"[red]{Escapar(texto)}[/]"))
        {
            Header = new PanelHeader("[red]Error[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Red)
        });
    }

    /// <summary>Dibuja la tabla de usuarios con su identificador completo y su presencia.</summary>
    /// <param name="usuarios">Usuarios a mostrar.</param>
    public static void TablaUsuarios(IReadOnlyList<UsuarioDto> usuarios)
    {
        ArgumentNullException.ThrowIfNull(usuarios);

        if (usuarios.Count == 0)
        {
            Aviso("No hay usuarios registrados.");
            return;
        }

        var conectados = usuarios.AsValueEnumerable().Count(u => u.EnLinea);

        var tabla = NuevaTabla($"Usuarios ({usuarios.Count}) · {conectados} en línea");

        tabla.AddColumn("[bold]Id[/]");
        tabla.AddColumn("[bold]Usuario[/]");
        tabla.AddColumn("[bold]Correo[/]");
        tabla.AddColumn("[bold]Alta[/]");
        tabla.AddColumn("[bold]Último acceso[/]");
        tabla.AddColumn(new TableColumn("[bold]Conexión[/]").Centered());
        tabla.AddColumn(new TableColumn("[bold]Estado[/]").Centered());

        foreach (var usuario in usuarios)
        {
            tabla.AddRow(
                $"[grey]{usuario.Id}[/]",
                $"[bold]{Escapar(usuario.NombreUsuario)}[/]",
                Escapar(usuario.Email),
                FechaCorta(usuario.FechaCreacion),
                usuario.FechaUltimoAcceso is null ? "[grey]nunca[/]" : FechaCorta(usuario.FechaUltimoAcceso.Value),
                Presencia(usuario.EnLinea),
                usuario.Activo ? "[green]activo[/]" : "[red]inactivo[/]");
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja la tabla de salas con su tipo y su identificador completo.</summary>
    /// <param name="salas">Salas a mostrar.</param>
    public static void TablaSalas(IReadOnlyList<SalaDto> salas)
    {
        ArgumentNullException.ThrowIfNull(salas);

        if (salas.Count == 0)
        {
            Aviso("No hay salas creadas.");
            return;
        }

        var tabla = NuevaTabla($"Salas ({salas.Count})");

        tabla.AddColumn("[bold]Id[/]");
        tabla.AddColumn("[bold]Nombre[/]");
        tabla.AddColumn(new TableColumn("[bold]Tipo[/]").Centered());
        tabla.AddColumn("[bold]Descripción[/]");
        tabla.AddColumn(new TableColumn("[bold]Miembros[/]").RightAligned());
        tabla.AddColumn("[bold]Última actividad[/]");

        foreach (var sala in salas)
        {
            tabla.AddRow(
                $"[grey]{sala.Id}[/]",
                NombreVisible(sala),
                EtiquetaTipo(sala.Tipo),
                Escapar(sala.Descripcion ?? "-"),
                sala.TotalMiembros.ToString(CultureInfo.InvariantCulture),
                sala.FechaUltimaActividad is null
                    ? "[grey]sin mensajes[/]"
                    : FechaCorta(sala.FechaUltimaActividad.Value));
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja la tabla de miembros de una sala.</summary>
    /// <param name="miembros">Miembros a mostrar.</param>
    /// <param name="nombreSala">Nombre de la sala.</param>
    public static void TablaMiembros(IReadOnlyList<MiembroSalaDto> miembros, string nombreSala)
    {
        ArgumentNullException.ThrowIfNull(miembros);

        if (miembros.Count == 0)
        {
            Aviso($"La sala '{nombreSala}' no tiene miembros.");
            return;
        }

        var tabla = NuevaTabla($"Miembros de {Escapar(nombreSala)} ({miembros.Count})");

        tabla.AddColumn("[bold]Id[/]");
        tabla.AddColumn("[bold]Usuario[/]");
        tabla.AddColumn("[bold]Desde[/]");
        tabla.AddColumn(new TableColumn("[bold]Conexión[/]").Centered());
        tabla.AddColumn(new TableColumn("[bold]Papel[/]").Centered());

        foreach (var miembro in miembros)
        {
            tabla.AddRow(
                $"[grey]{miembro.UsuarioId}[/]",
                $"[bold]{Escapar(miembro.NombreUsuario)}[/]",
                FechaCorta(miembro.FechaUnion),
                Presencia(miembro.EnLinea),
                miembro.EsCreador ? "[orange1]creador[/]" : "[grey]miembro[/]");
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja la tabla de mensajes de una sala.</summary>
    /// <param name="mensajes">Mensajes a mostrar.</param>
    /// <param name="nombreSala">Nombre de la sala.</param>
    public static void TablaMensajes(IReadOnlyList<MensajeDto> mensajes, string nombreSala)
    {
        ArgumentNullException.ThrowIfNull(mensajes);

        if (mensajes.Count == 0)
        {
            Aviso($"La sala '{nombreSala}' no tiene mensajes.");
            return;
        }

        var tabla = NuevaTabla($"Mensajes de {Escapar(nombreSala)} ({mensajes.Count})");
        tabla.Expand();

        tabla.AddColumn(new TableColumn("[bold]Fecha[/]").Width(20));
        tabla.AddColumn(new TableColumn("[bold]Usuario[/]").Width(20));
        tabla.AddColumn("[bold]Mensaje[/]");

        foreach (var mensaje in mensajes)
        {
            tabla.AddRow(
                $"[grey]{mensaje.FechaEnvio.ToLocalTime():yyyy-MM-dd HH:mm:ss}[/]",
                Escapar(mensaje.NombreUsuario),
                Escapar(mensaje.Texto));
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja el panel de estadísticas.</summary>
    /// <param name="estadisticas">Datos a mostrar.</param>
    public static void PanelEstadisticas(EstadisticasDto estadisticas)
    {
        ArgumentNullException.ThrowIfNull(estadisticas);

        var contenido = new Grid();
        contenido.AddColumn(new GridColumn().PadRight(4));
        contenido.AddColumn(new GridColumn().PadRight(4));
        contenido.AddColumn();

        contenido.AddRow(
            Tarjeta("Usuarios", estadisticas.TotalUsuarios, "deepskyblue1"),
            Tarjeta("Salas", estadisticas.TotalSalas, "mediumpurple2"),
            Tarjeta("Mensajes", estadisticas.TotalMensajes, "orange1"));

        contenido.AddRow(
            Tarjeta("Conexiones", estadisticas.ConexionesActivas, "green"),
            Tarjeta("En línea", estadisticas.UsuariosConectados, "green"),
            new Markup(string.Empty));

        AnsiConsole.Write(new Panel(contenido)
        {
            Header = new PanelHeader("[bold]Estado de la plataforma[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(ColorBorde),
            Padding = new Padding(2, 1, 2, 1)
        });

        AnsiConsole.MarkupLine(
            $"[grey]Consulta realizada a las {estadisticas.FechaConsulta.ToLocalTime():yyyy-MM-dd HH:mm:ss}.[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>Dibuja la tabla de presencia de los usuarios conocidos.</summary>
    /// <param name="presencias">Estados de conexión a mostrar.</param>
    public static void TablaPresencia(IReadOnlyList<PresenciaDto> presencias)
    {
        ArgumentNullException.ThrowIfNull(presencias);

        if (presencias.Count == 0)
        {
            Aviso("Ningún usuario se ha conectado desde que arrancó el servidor.");
            return;
        }

        var tabla = NuevaTabla("Presencia");

        tabla.AddColumn("[bold]Usuario[/]");
        tabla.AddColumn(new TableColumn("[bold]Estado[/]").Centered());
        tabla.AddColumn(new TableColumn("[bold]Conexiones[/]").RightAligned());
        tabla.AddColumn("[bold]Visto por última vez[/]");

        foreach (var presencia in presencias)
        {
            tabla.AddRow(
                $"[bold]{Escapar(presencia.NombreUsuario)}[/]",
                Presencia(presencia.EnLinea),
                presencia.Conexiones.ToString(CultureInfo.InvariantCulture),
                presencia.UltimaVez is null
                    ? "[grey]-[/]"
                    : $"[grey]{presencia.UltimaVez.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}[/]");
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja la tabla de conexiones activas.</summary>
    /// <param name="conexiones">Conexiones a mostrar.</param>
    public static void TablaConexiones(IReadOnlyList<ConexionActivaDto> conexiones)
    {
        ArgumentNullException.ThrowIfNull(conexiones);

        if (conexiones.Count == 0)
        {
            Aviso("No hay ninguna conexión activa en este momento.");
            return;
        }

        var tabla = NuevaTabla($"Conexiones activas ({conexiones.Count})");

        tabla.AddColumn("[bold]Conexión[/]");
        tabla.AddColumn("[bold]Usuario[/]");
        tabla.AddColumn("[bold]Desde[/]");
        tabla.AddColumn("[bold]Salas[/]");

        foreach (var conexion in conexiones)
        {
            tabla.AddRow(
                $"[grey]{Escapar(conexion.ConexionId)}[/]",
                $"[bold]{Escapar(conexion.NombreUsuario)}[/]",
                $"[grey]{conexion.FechaConexion.ToLocalTime():yyyy-MM-dd HH:mm:ss}[/]",
                conexion.Salas.Count == 0 ? "[grey]ninguna[/]" : Escapar(string.Join(", ", conexion.Salas)));
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Escapa el marcado de Spectre.Console en textos procedentes del servidor.</summary>
    /// <param name="texto">Texto original.</param>
    public static string Escapar(string texto) => Markup.Escape(texto);

    /// <summary>Crea una tabla con el estilo común de la consola administrativa.</summary>
    /// <param name="titulo">Título ya escapado.</param>
    private static Table NuevaTabla(string titulo) => new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(ColorBorde)
        .Title($"[bold]{titulo}[/]");

    /// <summary>Construye una tarjeta de métrica para el panel de estadísticas.</summary>
    /// <param name="titulo">Nombre de la métrica.</param>
    /// <param name="valor">Valor a destacar.</param>
    /// <param name="color">Color del valor.</param>
    private static Markup Tarjeta(string titulo, int valor, string color)
        => new($"[grey]{Escapar(titulo)}[/]\n[bold {color}]{Numero(valor)}[/]");

    /// <summary>
    /// Nombre con el que se lista una sala. El de una conversación directa es un
    /// identificador interno que no dice nada a nadie: se sustituye por una etiqueta
    /// y se localiza por su identificador o por sus miembros.
    /// </summary>
    /// <param name="sala">Sala a listar.</param>
    private static string NombreVisible(SalaDto sala) => sala.Tipo == TipoSala.Directa
        ? "[grey]conversación directa[/]"
        : $"[bold]{Escapar(sala.Nombre)}[/]";

    /// <summary>Etiqueta coloreada del tipo de sala.</summary>
    /// <param name="tipo">Naturaleza de la sala.</param>
    private static string EtiquetaTipo(TipoSala tipo) => tipo switch
    {
        TipoSala.Publica => "[green]pública[/]",
        TipoSala.Privada => "[yellow]privada[/]",
        _ => "[mediumpurple2]directa[/]"
    };

    /// <summary>Etiqueta coloreada del estado de conexión.</summary>
    /// <param name="enLinea">Indica si el usuario está conectado.</param>
    private static string Presencia(bool enLinea) => enLinea ? "[green]● en línea[/]" : "[grey]○ ausente[/]";

    /// <summary>Formatea una fecha en el formato corto usado por todas las tablas.</summary>
    /// <param name="fecha">Fecha en UTC.</param>
    private static string FechaCorta(DateTimeOffset fecha)
        => $"[grey]{fecha.ToLocalTime():yyyy-MM-dd HH:mm}[/]";

    /// <summary>Formatea un número con separador de millares.</summary>
    /// <param name="valor">Valor a formatear.</param>
    private static string Numero(int valor) => valor.ToString("N0", CultureInfo.CurrentCulture);
}
