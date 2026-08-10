using Chat.Aplicacion.Abstracciones;
using Chat.Aplicacion.Cqrs;
using Chat.Aplicacion.Dtos;
using Chat.Aplicacion.Mapeos;
using Chat.Aplicacion.Opciones;
using Chat.Dominio.Abstracciones;
using Chat.Dominio.Entidades;
using Microsoft.Extensions.Options;
using ZLinq;

namespace Chat.Aplicacion.Consultas.Salas;

/// <summary>
/// Lista el catálogo de salas visible para un usuario: todas las públicas más las
/// privadas a las que pertenece. Las conversaciones directas nunca aparecen aquí.
/// </summary>
/// <param name="SolicitanteId">Usuario que consulta el catálogo.</param>
/// <param name="IncluirTodas">
/// Devuelve además las salas privadas ajenas y las conversaciones directas.
/// Reservado a la auditoría administrativa.
/// </param>
public sealed record ConsultaListarSalas(Guid SolicitanteId, bool IncluirTodas = false)
    : IConsulta<IReadOnlyList<SalaDto>>;

/// <summary>
/// Manejador de <see cref="ConsultaListarSalas"/>. Cachea el catálogo completo de salas,
/// que se invalida al crear, eliminar o cambiar la composición de cualquier sala, y
/// aplica después el filtro de visibilidad, que depende de quién pregunte.
/// </summary>
public sealed class ManejadorListarSalas
    : IManejadorConsulta<ConsultaListarSalas, IReadOnlyList<SalaDto>>
{
    private readonly IRepositorioSalas _salas;
    private readonly IServicioCache _cache;
    private readonly CacheOptions _opciones;

    /// <summary>Crea el manejador.</summary>
    public ManejadorListarSalas(
        IRepositorioSalas salas,
        IServicioCache cache,
        IOptions<CacheOptions> opciones)
    {
        _salas = salas;
        _cache = cache;
        _opciones = opciones.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SalaDto>> ManejarAsync(
        ConsultaListarSalas consulta,
        CancellationToken cancelacion = default)
    {
        ArgumentNullException.ThrowIfNull(consulta);

        var catalogo = await _cache.ObtenerOCrearAsync<IReadOnlyList<SalaDto>>(
            ClavesCache.ListaSalas,
            async ct =>
            {
                var salas = await _salas.ListarAsync(ct).ConfigureAwait(false);

                // ZLinq proyecta sin materializar enumeradores intermedios: solo se
                // asigna el array final que queda guardado en la caché.
                return salas.AsValueEnumerable().Select(s => s.ADto(s.Miembros.Count)).ToArray();
            },
            TimeSpan.FromSeconds(_opciones.SegundosDuracionSalas),
            cancelacion).ConfigureAwait(false);

        var propias = consulta.SolicitanteId == Guid.Empty
            ? []
            : (await _salas
                .ListarSalasDeUsuarioAsync(consulta.SolicitanteId, cancelacion)
                .ConfigureAwait(false))
                .AsValueEnumerable()
                .ToHashSet();

        var visibles = new List<SalaDto>(catalogo.Count);

        foreach (var sala in catalogo)
        {
            var esMiembro = propias.Contains(sala.Id);

            if (consulta.IncluirTodas || EsVisible(sala.Tipo, esMiembro))
            {
                visibles.Add(sala with { EsMiembro = esMiembro });
            }
        }

        return visibles;
    }

    /// <summary>Decide si una sala aparece en el catálogo de quien consulta.</summary>
    /// <param name="tipo">Naturaleza de la sala.</param>
    /// <param name="esMiembro">Indica si quien consulta pertenece a ella.</param>
    private static bool EsVisible(TipoSala tipo, bool esMiembro) => tipo switch
    {
        TipoSala.Publica => true,
        TipoSala.Privada => esMiembro,
        _ => false
    };
}
