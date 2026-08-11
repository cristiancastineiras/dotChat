using Chat.Aplicacion.Dtos;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Chat.ClienteCli.Servicios;

/// <summary>
/// Aplicación interactiva del cliente: enlaza la lista de conversaciones con la
/// conversación abierta, igual que cualquier aplicación de mensajería.
/// </summary>
/// <remarks>
/// <para>
/// El flujo es siempre el mismo: se entra en la lista, se abre una conversación, se
/// vuelve a la lista. Todo lo demás —empezar un privado, unirse a una sala, crearla,
/// ver quién hay— son desvíos que acaban devolviendo a la lista.
/// </para>
/// <para>
/// La conexión con el hub se abre una sola vez, al principio, y se mantiene mientras la
/// aplicación viva. Así la lista recibe los mensajes que llegan aunque no haya ninguna
/// conversación abierta, que es lo que permite ver subir los contadores sin tocar nada.
/// </para>
/// </remarks>
public sealed class AplicacionChat
{
    private readonly ClienteApi _api;
    private readonly ClienteTiempoReal _tiempoReal;
    private readonly VistaConversacion _vista;
    private readonly PantallaListaChats _lista;
    private readonly OpcionesCliente _opciones;

    /// <summary>Crea la aplicación.</summary>
    /// <param name="api">Cliente de la API.</param>
    /// <param name="tiempoReal">Cliente de SignalR.</param>
    /// <param name="vista">Vista de conversación.</param>
    /// <param name="lista">Pantalla de lista de conversaciones.</param>
    /// <param name="opciones">Configuración del cliente.</param>
    public AplicacionChat(
        ClienteApi api,
        ClienteTiempoReal tiempoReal,
        VistaConversacion vista,
        PantallaListaChats lista,
        IOptions<OpcionesCliente> opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);

        _api = api;
        _tiempoReal = tiempoReal;
        _vista = vista;
        _lista = lista;
        _opciones = opciones.Value;
    }

    /// <summary>Ejecuta la aplicación hasta que el usuario la cierra.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>Código de salida del proceso.</returns>
    public async Task<int> EjecutarAsync(CancellationToken cancelacion = default)
    {
        var sesion = await AsegurarSesionAsync(cancelacion).ConfigureAwait(false);

        if (sesion is null)
        {
            return 1;
        }

        await ConectarAsync(cancelacion).ConfigureAwait(false);

        while (!cancelacion.IsCancellationRequested)
        {
            var resultado = await _lista.MostrarAsync(sesion, cancelacion).ConfigureAwait(false);

            if (resultado.Accion == AccionLista.Salir)
            {
                Presentacion.LimpiarPantalla();
                AnsiConsole.MarkupLine("[grey]Hasta luego.[/]");
                return 0;
            }

            await AtenderAsync(resultado, sesion, cancelacion).ConfigureAwait(false);

            // Se vuelva de donde se vuelva, la lista puede haber cambiado: mensajes
            // leídos, una sala nueva, un contador a cero.
            _lista.Invalidar();
        }

        return 0;
    }

    /// <summary>Ejecuta la acción elegida en la lista.</summary>
    /// <param name="resultado">Acción y conversación seleccionadas.</param>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task AtenderAsync(ResultadoLista resultado, SesionAlmacenada sesion, CancellationToken cancelacion)
    {
        try
        {
            switch (resultado.Accion)
            {
                case AccionLista.Abrir when resultado.Sala is not null:
                    await _vista.AbrirAsync(sesion, resultado.Sala, cancelacion).ConfigureAwait(false);
                    break;

                case AccionLista.NuevoPrivado:
                    await AbrirPrivadoAsync(sesion, cancelacion).ConfigureAwait(false);
                    break;

                case AccionLista.ExplorarSalas:
                    await ExplorarSalasAsync(sesion, cancelacion).ConfigureAwait(false);
                    break;

                case AccionLista.CrearSala:
                    await CrearSalaAsync(sesion, cancelacion).ConfigureAwait(false);
                    break;

                case AccionLista.VerUsuarios:
                    await VerUsuariosAsync(cancelacion).ConfigureAwait(false);
                    break;

                default:
                    break;
            }
        }
        catch (Exception excepcion) when (excepcion is not OperationCanceledException)
        {
            // Un fallo en una acción no debe cerrar la aplicación: se informa y se
            // vuelve a la lista.
            Presentacion.Error(excepcion.Message);
            Pausar();
        }
    }

    /// <summary>Carga la sesión guardada o pide credenciales.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    /// <returns>La sesión activa, o <c>null</c> si el usuario desistió.</returns>
    private async Task<SesionAlmacenada?> AsegurarSesionAsync(CancellationToken cancelacion)
    {
        var sesion = await _api.CargarSesionAsync(cancelacion).ConfigureAwait(false);

        if (sesion is not null)
        {
            return sesion;
        }

        Presentacion.LimpiarPantalla();
        Presentacion.Banner(_api.UrlServidor);

        var eleccion = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[grey]No hay ninguna sesión iniciada.[/]")
                .AddChoices("Iniciar sesión", "Crear una cuenta", "Salir"));

        if (eleccion == "Salir")
        {
            return null;
        }

        var usuario = AnsiConsole.Ask<string>("Usuario:");
        var clave = AnsiConsole.Prompt(new TextPrompt<string>("Contraseña:").Secret());

        return eleccion == "Iniciar sesión"
            ? await _api.IniciarSesionAsync(usuario, clave, cancelacion).ConfigureAwait(false)
            : await _api
                .RegistrarAsync(usuario, AnsiConsole.Ask<string>("Correo:"), clave, cancelacion)
                .ConfigureAwait(false);
    }

    /// <summary>Abre la conexión con el hub antes de entrar en la lista.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task ConectarAsync(CancellationToken cancelacion)
    {
        var configuracion = await _api.ObtenerConfiguracionAsync(cancelacion).ConfigureAwait(false);

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse(Presentacion.ColorPrincipal))
            .StartAsync(
                "Conectando...",
                async _ => await _tiempoReal.ConectarAsync(configuracion.RutaHub, cancelacion).ConfigureAwait(false))
            .ConfigureAwait(false);
    }

    /// <summary>Pregunta con quién hablar y abre la conversación directa.</summary>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task AbrirPrivadoAsync(SesionAlmacenada sesion, CancellationToken cancelacion)
    {
        Presentacion.LimpiarPantalla();
        Presentacion.Cabecera("Nueva conversación privada");

        var usuarios = await _api.ObtenerUsuariosAsync(cancelacion).ConfigureAwait(false);
        var candidatos = usuarios.Where(usuario => usuario.Id != sesion.UsuarioId).ToList();

        if (candidatos.Count == 0)
        {
            Presentacion.Aviso("No hay nadie más registrado en el servidor todavía.");
            Pausar();
            return;
        }

        var elegido = AnsiConsole.Prompt(
            new SelectionPrompt<UsuarioDto>()
                .Title("¿Con quién quieres hablar?")
                .PageSize(15)
                .MoreChoicesText("[grey](usa las flechas para ver más)[/]")
                .UseConverter(usuario => usuario.EnLinea
                    ? $"[green]●[/] {Presentacion.Escapar(usuario.NombreUsuario)}"
                    : $"[grey]○[/] {Presentacion.Escapar(usuario.NombreUsuario)}")
                .AddChoices(candidatos));

        var conversacion = await _api
            .AbrirConversacionDirectaAsync(elegido.NombreUsuario, cancelacion)
            .ConfigureAwait(false);

        await _vista.AbrirAsync(sesion, conversacion, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Muestra el catálogo de salas y permite entrar en una.</summary>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task ExplorarSalasAsync(SesionAlmacenada sesion, CancellationToken cancelacion)
    {
        Presentacion.LimpiarPantalla();
        Presentacion.Cabecera("Catálogo de salas");

        var catalogo = await _api.ObtenerSalasAsync(cancelacion).ConfigureAwait(false);
        var propias = await _api.ObtenerSalasPropiasAsync(cancelacion).ConfigureAwait(false);
        var identificadoresPropios = propias.Select(sala => sala.Id).ToHashSet();

        var disponibles = catalogo.Where(sala => !sala.EsDirecta).ToList();

        if (disponibles.Count == 0)
        {
            Presentacion.Aviso("No hay salas todavía. Crea una con la tecla 'c'.");
            Pausar();
            return;
        }

        Presentacion.TablaSalas(disponibles, identificadoresPropios);
        AnsiConsole.WriteLine();

        var elegida = AnsiConsole.Prompt(
            new SelectionPrompt<SalaDto>()
                .Title("¿En qué sala quieres entrar?")
                .PageSize(15)
                .MoreChoicesText("[grey](usa las flechas para ver más)[/]")
                .UseConverter(sala => identificadoresPropios.Contains(sala.Id)
                    ? $"{Presentacion.Escapar(sala.Nombre)} [grey](ya dentro)[/]"
                    : Presentacion.Escapar(sala.Nombre))
                .AddChoices(disponibles));

        var sala = identificadoresPropios.Contains(elegida.Id)
            ? elegida
            : await _api.UnirseSalaAsync(elegida.Id, cancelacion).ConfigureAwait(false);

        await _vista.AbrirAsync(sesion, sala, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Pide los datos de una sala nueva y la crea.</summary>
    /// <param name="sesion">Sesión del usuario.</param>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task CrearSalaAsync(SesionAlmacenada sesion, CancellationToken cancelacion)
    {
        Presentacion.LimpiarPantalla();
        Presentacion.Cabecera("Crear una sala");

        var nombre = AnsiConsole.Ask<string>("Nombre de la sala:");

        var descripcion = AnsiConsole.Prompt(
            new TextPrompt<string>("Descripción [grey](opcional)[/]:").AllowEmpty());

        var privada = AnsiConsole.Confirm("¿Privada? (solo entra quien sea invitado)", defaultValue: false);

        var sala = await _api
            .CrearSalaAsync(nombre, string.IsNullOrWhiteSpace(descripcion) ? null : descripcion, privada, cancelacion)
            .ConfigureAwait(false);

        Presentacion.Exito($"Sala '{sala.Nombre}' creada.");

        await _vista.AbrirAsync(sesion, sala, cancelacion).ConfigureAwait(false);
    }

    /// <summary>Muestra la lista de usuarios y su presencia.</summary>
    /// <param name="cancelacion">Token de cancelación.</param>
    private async Task VerUsuariosAsync(CancellationToken cancelacion)
    {
        Presentacion.LimpiarPantalla();
        Presentacion.Cabecera("Usuarios de la plataforma");

        var usuarios = await _api.ObtenerUsuariosAsync(cancelacion).ConfigureAwait(false);
        Presentacion.TablaUsuarios(usuarios);

        Pausar();
    }

    /// <summary>Espera una tecla antes de volver a la lista, para que dé tiempo a leer.</summary>
    private static void Pausar()
    {
        AnsiConsole.MarkupLine("[grey]Pulsa cualquier tecla para volver.[/]");

        if (!Console.IsInputRedirected)
        {
            Console.ReadKey(intercept: true);
        }
    }
}
