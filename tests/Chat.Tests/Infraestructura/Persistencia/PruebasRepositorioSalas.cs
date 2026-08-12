using Chat.Dominio.Entidades;
using Chat.Tests.Comun;
using Microsoft.EntityFrameworkCore;

namespace Chat.Tests.Infraestructura.Persistencia;

/// <summary>Pruebas del repositorio de salas contra una base de datos relacional real.</summary>
public sealed class PruebasRepositorioSalas : IDisposable
{
    private readonly BaseDatosDePrueba _bd = new();

    /// <inheritdoc />
    public void Dispose() => _bd.Dispose();

    [Fact]
    public async Task UnaSalaSeRecuperaPorSuIdentificador()
    {
        var sala = Datos.Sala();
        await _bd.SembrarAsync(sala);
        _bd.Olvidar();

        var recuperada = await _bd.Salas.ObtenerPorIdAsync(sala.Id);

        Assert.NotNull(recuperada);
        Assert.Equal("General", recuperada.Nombre);
    }

    [Fact]
    public async Task UnIdentificadorDesconocidoDevuelveNulo()
        => Assert.Null(await _bd.Salas.ObtenerPorIdAsync(Guid.CreateVersion7()));

    [Fact]
    public async Task LaBusquedaPorNombreNoDistingueMayusculas()
    {
        // La columna usa una intercalación insensible: es lo que impide que convivan
        // «General» y «general» como salas distintas.
        await _bd.SembrarAsync(Datos.Sala(nombre: "General"));
        _bd.Olvidar();

        Assert.NotNull(await _bd.Salas.ObtenerPorNombreAsync("general"));
        Assert.NotNull(await _bd.Salas.ObtenerPorNombreAsync("GENERAL"));
    }

    [Fact]
    public async Task UnaConversacionDirectaSeRecuperaPorSuClaveCanonica()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var eva = await _bd.SembrarUsuarioAsync("eva");
        var directa = Datos.SalaDirecta(ana, eva);

        await _bd.SembrarAsync(directa);
        _bd.Olvidar();

        // El orden de los participantes es indiferente: la clave es la misma.
        var clave = Sala.ConstruirClaveDirecta(eva.Id, ana.Id);
        var recuperada = await _bd.Salas.ObtenerPorClaveDirectaAsync(clave);

        Assert.NotNull(recuperada);
        Assert.Equal(directa.Id, recuperada.Id);
    }

    [Fact]
    public async Task ElCatalogoSeDevuelveOrdenadoPorNombreYConSusMiembros()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var zeta = Datos.Sala(nombre: "Zeta");
        var alfa = Datos.Sala(nombre: "Alfa");

        await _bd.SembrarAsync(zeta, alfa);
        await _bd.SembrarAsync(Datos.Membresia(alfa.Id, ana.Id));
        _bd.Olvidar();

        var catalogo = await _bd.Salas.ListarAsync();

        Assert.Equal(["Alfa", "Zeta"], catalogo.Select(s => s.Nombre));
        Assert.Single(catalogo[0].Miembros);
        Assert.Empty(catalogo[1].Miembros);
    }

    [Fact]
    public async Task LaPertenenciaSeConsultaYSeRegistra()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var sala = Datos.Sala();
        await _bd.SembrarAsync(sala);

        Assert.False(await _bd.Salas.EsMiembroAsync(sala.Id, ana.Id));

        await _bd.Salas.AgregarMembresiaAsync(Datos.Membresia(sala.Id, ana.Id));
        await _bd.UnidadDeTrabajo.GuardarCambiosAsync();
        _bd.Olvidar();

        Assert.True(await _bd.Salas.EsMiembroAsync(sala.Id, ana.Id));
        Assert.Equal(1, await _bd.Salas.ContarMiembrosAsync(sala.Id));
    }

    [Fact]
    public async Task AlEliminarUnaMembresiaElUsuarioDejaDeSerMiembro()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var sala = Datos.Sala();
        await _bd.SembrarAsync(sala);
        await _bd.SembrarAsync(Datos.Membresia(sala.Id, ana.Id));

        var membresia = await _bd.Salas.ObtenerMembresiaAsync(sala.Id, ana.Id);
        Assert.NotNull(membresia);

        _bd.Salas.EliminarMembresia(membresia);
        await _bd.UnidadDeTrabajo.GuardarCambiosAsync();
        _bd.Olvidar();

        Assert.False(await _bd.Salas.EsMiembroAsync(sala.Id, ana.Id));
    }

    [Fact]
    public async Task AlEliminarUnaSalaSeVanConEllaSusMensajesYSusMembresias()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var sala = Datos.Sala();

        await _bd.SembrarAsync(sala);
        await _bd.SembrarAsync(Datos.Membresia(sala.Id, ana.Id), Datos.Mensaje(sala.Id, ana.Id));

        var recuperada = await _bd.Salas.ObtenerPorIdAsync(sala.Id);
        _bd.Salas.Eliminar(recuperada!);
        await _bd.UnidadDeTrabajo.GuardarCambiosAsync();
        _bd.Olvidar();

        using var comprobacion = _bd.CrearContexto();
        Assert.Equal(0, await comprobacion.Salas.CountAsync());
        Assert.Equal(0, await comprobacion.MiembrosSala.CountAsync());
        Assert.Equal(0, await comprobacion.Mensajes.CountAsync());
    }

    [Fact]
    public async Task LosMiembrosSeListanConSuUsuarioCargadoYPorOrdenDeEntrada()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var eva = await _bd.SembrarUsuarioAsync("eva");
        var sala = Datos.Sala();
        await _bd.SembrarAsync(sala);

        var primera = Datos.Membresia(sala.Id, eva.Id);
        primera.FechaUnion = Datos.Ahora;

        var segunda = Datos.Membresia(sala.Id, ana.Id);
        segunda.FechaUnion = Datos.Ahora.AddMinutes(5);

        await _bd.SembrarAsync(primera, segunda);
        _bd.Olvidar();

        var miembros = await _bd.Salas.ListarMiembrosAsync(sala.Id);

        Assert.Equal(["eva", "ana"], miembros.Select(m => m.Usuario!.UserName));
    }

    [Fact]
    public async Task LaBandejaDelUsuarioLlegaOrdenadaPorActividadReciente()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");

        var antigua = Datos.Sala(nombre: "Antigua");
        antigua.FechaUltimaActividad = Datos.Ahora.AddHours(-3);

        var reciente = Datos.Sala(nombre: "Reciente");
        reciente.FechaUltimaActividad = Datos.Ahora;

        var ajena = Datos.Sala(nombre: "Ajena");

        await _bd.SembrarAsync(antigua, reciente, ajena);
        await _bd.SembrarAsync(
            Datos.Membresia(antigua.Id, ana.Id),
            Datos.Membresia(reciente.Id, ana.Id));
        _bd.Olvidar();

        var bandeja = await _bd.Salas.ListarDeUsuarioAsync(ana.Id);

        Assert.Equal(["Reciente", "Antigua"], bandeja.Select(s => s.Nombre));

        // La bandeja alimenta la lista de chats, que necesita los miembros y sus
        // usuarios cargados para poder nombrar cada conversación directa.
        Assert.All(bandeja, sala => Assert.All(sala.Miembros, miembro => Assert.NotNull(miembro.Usuario)));
    }

    [Fact]
    public async Task UnaSalaSinActividadSeOrdenaPorSuFechaDeCreacion()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");

        var nueva = Datos.Sala(nombre: "Nueva");
        nueva.FechaCreacion = Datos.Ahora;

        var vieja = Datos.Sala(nombre: "Vieja");
        vieja.FechaCreacion = Datos.Ahora.AddDays(-2);

        await _bd.SembrarAsync(nueva, vieja);
        await _bd.SembrarAsync(Datos.Membresia(nueva.Id, ana.Id), Datos.Membresia(vieja.Id, ana.Id));
        _bd.Olvidar();

        var bandeja = await _bd.Salas.ListarDeUsuarioAsync(ana.Id);

        Assert.Equal(["Nueva", "Vieja"], bandeja.Select(s => s.Nombre));
    }

    [Fact]
    public async Task SeCuentanLasSalasYSeListanLasDelUsuario()
    {
        var ana = await _bd.SembrarUsuarioAsync("ana");
        var propia = Datos.Sala(nombre: "Propia");
        var ajena = Datos.Sala(nombre: "Ajena");

        await _bd.SembrarAsync(propia, ajena);
        await _bd.SembrarAsync(Datos.Membresia(propia.Id, ana.Id));
        _bd.Olvidar();

        Assert.Equal(2, await _bd.Salas.ContarAsync());
        Assert.Equal([propia.Id], await _bd.Salas.ListarSalasDeUsuarioAsync(ana.Id));
    }
}
