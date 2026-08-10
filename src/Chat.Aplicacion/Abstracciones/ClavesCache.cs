namespace Chat.Aplicacion.Abstracciones;

/// <summary>
/// Claves y etiquetas de caché centralizadas. Evita cadenas repetidas y hace
/// evidente qué se invalida al modificar cada agregado.
/// </summary>
public static class ClavesCache
{
    /// <summary>Etiqueta que agrupa todas las entradas relativas a usuarios.</summary>
    public const string EtiquetaUsuarios = "usuarios";

    /// <summary>Etiqueta que agrupa todas las entradas relativas a salas.</summary>
    public const string EtiquetaSalas = "salas";

    /// <summary>Etiqueta que agrupa las entradas de configuración de la plataforma.</summary>
    public const string EtiquetaConfiguracion = "configuracion";

    /// <summary>Clave del listado completo de usuarios.</summary>
    /// <param name="incluirInactivos">Variante del listado solicitada.</param>
    public static string ListaUsuarios(bool incluirInactivos)
        => $"usuarios:lista:{(incluirInactivos ? "todos" : "activos")}";

    /// <summary>Clave de la ficha de un usuario.</summary>
    /// <param name="usuarioId">Identificador del usuario.</param>
    public static string Usuario(Guid usuarioId) => $"usuarios:ficha:{usuarioId}";

    /// <summary>Clave del listado completo de salas.</summary>
    public const string ListaSalas = "salas:lista";

    /// <summary>Clave de la ficha de una sala.</summary>
    /// <param name="salaId">Identificador de la sala.</param>
    public static string Sala(Guid salaId) => $"salas:ficha:{salaId}";

    /// <summary>Clave de la configuración pública expuesta a los clientes.</summary>
    public const string ConfiguracionPlataforma = "configuracion:plataforma";

    /// <summary>Clave de un identificador de operación ya procesado (antirrepetición).</summary>
    /// <param name="ambito">Ámbito de la operación.</param>
    /// <param name="identificador">Identificador único de la operación.</param>
    public static string Repeticion(string ambito, string identificador) => $"repeticion:{ambito}:{identificador}";
}
