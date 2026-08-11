using System.Globalization;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mensajeria;
using Chat.Dominio.Entidades;
using Cysharp.Text;
using Spectre.Console;
using ZLinq;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Utilidades de presentación en consola. Centraliza estilos, tablas y paneles
/// para que todos los comandos tengan el mismo aspecto.
/// </summary>
public static class Presentacion
{
    /// <summary>Color principal de la interfaz del cliente.</summary>
    public const string ColorPrincipal = "deepskyblue1";

    /// <summary>Colores asignados a cada usuario, para reconocerlo de un vistazo.</summary>
    private static readonly string[] ColoresUsuario =
        ["cyan", "green", "yellow", "magenta", "blue", "orange1", "aquamarine1", "plum2"];

    /// <summary>
    /// Limpia la pantalla solo si hay un terminal real detrás. Con la salida
    /// redirigida (tuberías, ficheros de registro) no hay nada que limpiar y la
    /// llamada fallaría con «controlador no válido».
    /// </summary>
    public static void LimpiarPantalla()
    {
        if (!Console.IsOutputRedirected)
        {
            AnsiConsole.Clear();
        }
    }

    /// <summary>Dibuja el rótulo de bienvenida de la consola interactiva.</summary>
    /// <param name="servidor">Dirección del servidor al que apunta el cliente.</param>
    public static void Banner(string servidor)
    {
        AnsiConsole.Write(new FigletText("dotChat").LeftJustified().Color(Color.DeepSkyBlue1));
        AnsiConsole.MarkupLine($"[grey]Mensajería local cifrada · {Escapar(servidor)}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>Dibuja la cabecera de la aplicación.</summary>
    /// <param name="subtitulo">Texto secundario mostrado bajo el título.</param>
    public static void Cabecera(string subtitulo)
    {
        AnsiConsole.Write(new Rule($"[bold {ColorPrincipal}]dotChat[/]").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]{Escapar(subtitulo)}[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>Muestra un mensaje de éxito.</summary>
    /// <param name="texto">Contenido del mensaje.</param>
    public static void Exito(string texto)
        => AnsiConsole.MarkupLine($"[green]✔[/] {Escapar(texto)}");

    /// <summary>Muestra un aviso.</summary>
    /// <param name="texto">Contenido del aviso.</param>
    public static void Aviso(string texto)
        => AnsiConsole.MarkupLine($"[yellow]![/] {Escapar(texto)}");

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

    /// <summary>Dibuja la tabla del catálogo de salas.</summary>
    /// <param name="salas">Salas a mostrar.</param>
    /// <param name="propias">Identificadores de las salas a las que pertenece el usuario.</param>
    public static void TablaSalas(IReadOnlyList<SalaDto> salas, IReadOnlySet<Guid>? propias = null)
    {
        ArgumentNullException.ThrowIfNull(salas);

        if (salas.Count == 0)
        {
            Aviso("No hay salas disponibles todavía. Cree una con 'salas --crear <nombre>'.");
            return;
        }

        var tabla = NuevaTabla($"Salas ({salas.Count})");

        tabla.AddColumn("[bold]Nombre[/]");
        tabla.AddColumn(new TableColumn("[bold]Tipo[/]").Centered());
        tabla.AddColumn("[bold]Descripción[/]");
        tabla.AddColumn(new TableColumn("[bold]Miembros[/]").RightAligned());
        tabla.AddColumn("[bold]Última actividad[/]");

        if (propias is not null)
        {
            tabla.AddColumn(new TableColumn("[bold]Unido[/]").Centered());
        }

        foreach (var sala in salas)
        {
            var celdas = new List<string>(6)
            {
                $"[{ColorPrincipal}]{Escapar(sala.Nombre)}[/]",
                EtiquetaTipo(sala.Tipo),
                Escapar(sala.Descripcion ?? "-"),
                sala.TotalMiembros.ToString(CultureInfo.InvariantCulture),
                Actividad(sala.FechaUltimaActividad)
            };

            if (propias is not null)
            {
                celdas.Add(propias.Contains(sala.Id) ? "[green]sí[/]" : "[grey]no[/]");
            }

            tabla.AddRow([.. celdas]);
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja la bandeja del usuario: sus salas y conversaciones con lo pendiente.</summary>
    /// <param name="salas">Salas propias.</param>
    public static void TablaBandeja(IReadOnlyList<SalaDto> salas)
    {
        ArgumentNullException.ThrowIfNull(salas);

        if (salas.Count == 0)
        {
            Aviso("Aún no perteneces a ninguna sala. Únete con 'unirse <sala>' o escribe a alguien con 'privado <usuario>'.");
            return;
        }

        var pendientes = salas.AsValueEnumerable().Sum(s => s.MensajesSinLeer);
        var titulo = pendientes == 0
            ? $"Tus conversaciones ({salas.Count})"
            : $"Tus conversaciones ({salas.Count}) · {pendientes} sin leer";

        var tabla = NuevaTabla(titulo);

        tabla.AddColumn("[bold]Conversación[/]");
        tabla.AddColumn(new TableColumn("[bold]Tipo[/]").Centered());
        tabla.AddColumn(new TableColumn("[bold]Sin leer[/]").Centered());
        tabla.AddColumn(new TableColumn("[bold]Miembros[/]").RightAligned());
        tabla.AddColumn("[bold]Última actividad[/]");

        foreach (var sala in salas)
        {
            tabla.AddRow(
                $"[{ColorPrincipal}]{Escapar(sala.Nombre)}[/]",
                EtiquetaTipo(sala.Tipo),
                sala.MensajesSinLeer == 0
                    ? "[grey]-[/]"
                    : $"[bold red]● {sala.MensajesSinLeer}[/]",
                sala.TotalMiembros.ToString(CultureInfo.InvariantCulture),
                Actividad(sala.FechaUltimaActividad));
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja la tabla de usuarios con su estado de conexión.</summary>
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

        tabla.AddColumn("[bold]Usuario[/]");
        tabla.AddColumn(new TableColumn("[bold]Conexión[/]").Centered());
        tabla.AddColumn("[bold]Correo[/]");
        tabla.AddColumn("[bold]Alta[/]");
        tabla.AddColumn("[bold]Último acceso[/]");

        foreach (var usuario in usuarios)
        {
            tabla.AddRow(
                $"[{ColorDe(usuario.NombreUsuario)}]{Escapar(usuario.NombreUsuario)}[/]",
                Presencia(usuario.EnLinea),
                Escapar(usuario.Email),
                $"[grey]{usuario.FechaCreacion.ToLocalTime():yyyy-MM-dd}[/]",
                usuario.FechaUltimoAcceso is null
                    ? "[grey]nunca[/]"
                    : $"[grey]{usuario.FechaUltimoAcceso.Value.ToLocalTime():yyyy-MM-dd HH:mm}[/]");
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja la lista de miembros de una sala.</summary>
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

        var conectados = miembros.AsValueEnumerable().Count(m => m.EnLinea);
        var tabla = NuevaTabla($"Miembros de {Escapar(nombreSala)} ({miembros.Count}) · {conectados} en línea");

        tabla.AddColumn("[bold]Usuario[/]");
        tabla.AddColumn(new TableColumn("[bold]Conexión[/]").Centered());
        tabla.AddColumn("[bold]Desde[/]");
        tabla.AddColumn(new TableColumn("[bold]Papel[/]").Centered());

        foreach (var miembro in miembros)
        {
            tabla.AddRow(
                $"[{ColorDe(miembro.NombreUsuario)}]{Escapar(miembro.NombreUsuario)}[/]",
                Presencia(miembro.EnLinea),
                $"[grey]{miembro.FechaUnion.ToLocalTime():yyyy-MM-dd HH:mm}[/]",
                miembro.EsCreador ? $"[{ColorPrincipal}]creador[/]" : "[grey]miembro[/]");
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja la tabla del historial de mensajes.</summary>
    /// <param name="mensajes">Mensajes a mostrar.</param>
    /// <param name="nombreSala">Nombre de la sala consultada.</param>
    public static void TablaMensajes(IReadOnlyList<MensajeDto> mensajes, string nombreSala)
    {
        ArgumentNullException.ThrowIfNull(mensajes);

        if (mensajes.Count == 0)
        {
            Aviso($"La sala '{nombreSala}' aún no tiene mensajes.");
            return;
        }

        var tabla = NuevaTabla($"Historial de {Escapar(nombreSala)}");
        tabla.Expand();

        tabla.AddColumn(new TableColumn("[bold]Hora[/]").Width(18));
        tabla.AddColumn(new TableColumn("[bold]Usuario[/]").Width(20));
        tabla.AddColumn("[bold]Mensaje[/]");

        foreach (var mensaje in mensajes)
        {
            // En una tabla no se dibujan las imágenes: se anuncian con su ficha, que
            // es lo que cabe en una celda sin romper la alineación de las columnas.
            var contenido = mensaje.Adjunto is { } adjunto
                ? $"{FichaAdjunto(adjunto)} {Escapar(mensaje.Texto)}".TrimEnd()
                : Escapar(mensaje.Texto);

            tabla.AddRow(
                $"[grey]{mensaje.FechaEnvio.ToLocalTime():yyyy-MM-dd HH:mm}[/]",
                $"[{ColorDe(mensaje.NombreUsuario)}]{Escapar(mensaje.NombreUsuario)}[/]",
                contenido);
        }

        AnsiConsole.Write(tabla);
    }

    /// <summary>Dibuja la cabecera de una conversación abierta.</summary>
    /// <param name="sala">Sala abierta.</param>
    /// <param name="nombreUsuario">Nombre del usuario conectado.</param>
    public static void CabeceraConversacion(SalaDto sala, string nombreUsuario)
    {
        ArgumentNullException.ThrowIfNull(sala);

        var descripcion = sala.EsDirecta
            ? "Conversación privada, cifrada de extremo a servidor."
            : sala.Descripcion ?? "Sin descripción.";

        var contenido = new Markup(
            $"[bold {ColorPrincipal}]{Escapar(sala.Nombre)}[/] {EtiquetaTipo(sala.Tipo)}\n" +
            $"[grey]{Escapar(descripcion)}[/]\n" +
            $"[grey]{sala.TotalMiembros} miembro(s) · conectado como[/] [bold]{Escapar(nombreUsuario)}[/]");

        AnsiConsole.Write(new Panel(contenido)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey37),
            Padding = new Padding(1, 0, 1, 0),
            Expand = true
        });
    }

    /// <summary>Muestra las órdenes disponibles dentro de una conversación.</summary>
    public static void AyudaConversacion()
    {
        var tabla = new Table()
            .Border(TableBorder.None)
            .HideHeaders();

        tabla.AddColumn(new TableColumn(string.Empty).Width(14));
        tabla.AddColumn(string.Empty);

        tabla.AddRow("[bold]/salir[/]", "[grey]cierra la conversación y vuelve a la consola[/]");
        tabla.AddRow("[bold]/miembros[/]", "[grey]muestra quién está en la sala y quién está en línea[/]");
        tabla.AddRow("[bold]/historial[/]", "[grey]vuelve a cargar los mensajes recientes[/]");
        tabla.AddRow("[bold]/invitar[/] <usuario>", "[grey]incorpora a alguien a la sala[/]");
        tabla.AddRow("[bold]/archivo[/] <ruta> [texto]", "[grey]comparte un archivo; las imágenes se dibujan solas[/]");
        tabla.AddRow("[bold]/adjuntos[/]", "[grey]lista los archivos compartidos en la conversación[/]");
        tabla.AddRow("[bold]/descargar[/] <n>", "[grey]guarda en disco el enésimo archivo (1 es el último)[/]");
        tabla.AddRow("[bold]/ver[/] <n>", "[grey]vuelve a dibujar la enésima imagen recibida[/]");
        tabla.AddRow("[bold]/emojis[/]", "[grey]lista los atajos :nombre: que se pueden escribir[/]");
        tabla.AddRow("[bold]/limpiar[/]", "[grey]borra la pantalla sin perder la conexión[/]");
        tabla.AddRow("[bold]/ayuda[/]", "[grey]muestra esta lista[/]");

        AnsiConsole.Write(tabla);
    }

    /// <summary>
    /// Imprime un mensaje individual en el modo de conversación en vivo.
    /// </summary>
    /// <remarks>
    /// Es la única ruta que se ejecuta una vez por mensaje recibido, así que la línea
    /// se compone con el constructor de cadenas de ZString: usa memoria agrupada y
    /// produce una sola cadena final en lugar de una concatenación por tramo.
    /// </remarks>
    /// <param name="mensaje">Mensaje recibido.</param>
    /// <param name="esPropio">Indica si lo envió el usuario actual.</param>
    public static void LineaMensaje(MensajeDto mensaje, bool esPropio)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        using var constructor = ZString.CreateStringBuilder();

        constructor.Append("[grey]");
        constructor.Append(mensaje.FechaEnvio.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        constructor.Append("[/] [");
        constructor.Append(esPropio ? "bold white" : ColorDe(mensaje.NombreUsuario));
        constructor.Append(']');
        constructor.Append(Escapar(mensaje.NombreUsuario));
        constructor.Append("[/][grey]:[/] ");

        if (mensaje.Adjunto is { } adjunto)
        {
            // La ficha se imprime siempre, se dibuje la imagen o no: deja constancia
            // en el historial de qué se compartió y con qué tamaño.
            constructor.Append(FichaAdjunto(adjunto));

            if (mensaje.Texto.Length > 0)
            {
                constructor.Append(' ');
            }
        }

        constructor.Append(Escapar(mensaje.Texto));

        AnsiConsole.MarkupLine(constructor.ToString());
    }

    /// <summary>Compone la ficha de un adjunto: icono, nombre, dimensiones y peso.</summary>
    /// <param name="adjunto">Adjunto anunciado con el mensaje.</param>
    public static string FichaAdjunto(AdjuntoDto adjunto)
    {
        ArgumentNullException.ThrowIfNull(adjunto);

        var detalle = adjunto is { EsImagen: true, Ancho: { } ancho, Alto: { } alto }
            ? $"{ancho}×{alto}, {FormatearTamano(adjunto.TamanoBytes)}"
            : FormatearTamano(adjunto.TamanoBytes);

        return ZString.Concat(
            "[mediumpurple2]",
            adjunto.EsImagen ? "🖼 " : "📎 ",
            Escapar(adjunto.NombreArchivo),
            "[/] [grey](",
            detalle,
            ")[/]");
    }

    /// <summary>Dibuja la lista de archivos compartidos en la conversación.</summary>
    /// <param name="adjuntos">Adjuntos vistos, del más reciente al más antiguo.</param>
    public static void TablaAdjuntos(IReadOnlyList<AdjuntoDto> adjuntos)
    {
        ArgumentNullException.ThrowIfNull(adjuntos);

        if (adjuntos.Count == 0)
        {
            Aviso("Todavía no se ha compartido ningún archivo en esta conversación.");
            return;
        }

        var tabla = NuevaTabla($"Archivos compartidos ({adjuntos.Count})");

        tabla.AddColumn(new TableColumn("[bold]#[/]").RightAligned());
        tabla.AddColumn("[bold]Archivo[/]");
        tabla.AddColumn("[bold]Tipo[/]");
        tabla.AddColumn(new TableColumn("[bold]Tamaño[/]").RightAligned());

        for (var indice = 0; indice < adjuntos.Count; indice++)
        {
            var adjunto = adjuntos[indice];

            tabla.AddRow(
                $"[{ColorPrincipal}]{indice + 1}[/]",
                $"{(adjunto.EsImagen ? "🖼" : "📎")} {Escapar(adjunto.NombreArchivo)}",
                $"[grey]{Escapar(adjunto.TipoMime)}[/]",
                $"[grey]{FormatearTamano(adjunto.TamanoBytes)}[/]");
        }

        AnsiConsole.Write(tabla);
        AnsiConsole.MarkupLine(
            "[grey]Use [bold]/descargar <n>[/] para guardar uno en disco " +
            "o [bold]/ver <n>[/] para volver a dibujar una imagen.[/]");
    }

    /// <summary>Dibuja el catálogo de atajos de emoji agrupado por categoría.</summary>
    public static void TablaEmojis()
    {
        var tabla = NuevaTabla($"Emojis disponibles ({CatalogoEmojis.Entradas.Count})");

        tabla.AddColumn("[bold]Categoría[/]");
        tabla.AddColumn("[bold]Atajos[/]");

        foreach (var grupo in CatalogoEmojis.PorCategoria())
        {
            var atajos = string.Join(
                "  ",
                grupo.Select(entrada => $"{entrada.Simbolo} [grey]:{Escapar(entrada.Nombre)}:[/]"));

            tabla.AddRow($"[{ColorPrincipal}]{Escapar(grupo.Key)}[/]", atajos);
        }

        AnsiConsole.Write(tabla);
        AnsiConsole.MarkupLine(
            "[grey]Escriba el atajo entre dos puntos —por ejemplo [bold]:fuego:[/]— y el servidor lo sustituye " +
            "al enviar. También puede pegar el emoji directamente.[/]");
    }

    /// <summary>Expresa un tamaño en bytes con la unidad más legible.</summary>
    /// <param name="bytes">Tamaño a formatear.</param>
    public static string FormatearTamano(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KiB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MiB"
    };

    /// <summary>Imprime un aviso del sistema dentro de la conversación.</summary>
    /// <param name="texto">Texto del aviso.</param>
    public static void LineaSistema(string texto)
        => AnsiConsole.MarkupLine($"[grey]·· {Escapar(texto)}[/]");

    /// <summary>Imprime un aviso de presencia dentro de la conversación.</summary>
    /// <param name="nombreUsuario">Usuario cuyo estado ha cambiado.</param>
    /// <param name="enLinea">Nuevo estado.</param>
    public static void LineaPresencia(string nombreUsuario, bool enLinea)
    {
        var color = enLinea ? "green" : "grey";
        var estado = enLinea ? "se ha conectado" : "se ha desconectado";

        AnsiConsole.MarkupLine(
            $"[{color}]··[/] [{ColorDe(nombreUsuario)}]{Escapar(nombreUsuario)}[/] [grey]{estado}.[/]");
    }

    /// <summary>Muestra los datos de la sesión iniciada.</summary>
    /// <param name="sesion">Sesión activa.</param>
    public static void PanelSesion(SesionAlmacenada sesion)
    {
        ArgumentNullException.ThrowIfNull(sesion);

        var contenido = new Markup(
            $"[bold]Usuario:[/] {Escapar(sesion.NombreUsuario)}\n" +
            $"[bold]Roles:[/] {Escapar(string.Join(", ", sesion.Roles))}\n" +
            $"[bold]Servidor:[/] {Escapar(sesion.UrlServidor)}\n" +
            $"[bold]Sesión válida hasta:[/] {sesion.ExpiraEn.ToLocalTime():yyyy-MM-dd HH:mm:ss}");

        AnsiConsole.Write(new Panel(contenido)
        {
            Header = new PanelHeader("[green]Sesión iniciada[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green)
        });
    }

    /// <summary>Asigna de forma estable un color a cada nombre de usuario.</summary>
    /// <param name="nombreUsuario">Nombre del usuario.</param>
    public static string ColorDe(string nombreUsuario)
    {
        var indice = Math.Abs(StringComparer.OrdinalIgnoreCase.GetHashCode(nombreUsuario)) % ColoresUsuario.Length;
        return ColoresUsuario[indice];
    }

    /// <summary>
    /// Escapa el marcado de Spectre.Console. Evita que un texto enviado por otro
    /// usuario altere los colores o inyecte etiquetas en la consola.
    /// </summary>
    /// <param name="texto">Texto original.</param>
    public static string Escapar(string texto) => Markup.Escape(texto);

    /// <summary>Etiqueta coloreada del tipo de sala.</summary>
    /// <param name="tipo">Naturaleza de la sala.</param>
    public static string EtiquetaTipo(TipoSala tipo) => tipo switch
    {
        TipoSala.Publica => "[green]pública[/]",
        TipoSala.Privada => "[yellow]privada[/]",
        _ => "[mediumpurple2]privada 1:1[/]"
    };

    /// <summary>Crea una tabla con el estilo común del cliente.</summary>
    /// <param name="titulo">Título ya escapado.</param>
    private static Table NuevaTabla(string titulo) => new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Grey37)
        .Title($"[bold]{titulo}[/]");

    /// <summary>Etiqueta coloreada del estado de conexión.</summary>
    /// <param name="enLinea">Indica si el usuario está conectado.</param>
    private static string Presencia(bool enLinea) => enLinea ? "[green]● en línea[/]" : "[grey]○ ausente[/]";

    /// <summary>Formatea la marca de última actividad de una sala.</summary>
    /// <param name="fecha">Fecha del último mensaje, si lo hay.</param>
    private static string Actividad(DateTimeOffset? fecha)
        => fecha is null
            ? "[grey]sin mensajes[/]"
            : $"[grey]{fecha.Value.ToLocalTime():yyyy-MM-dd HH:mm}[/]";
}
