using System.Globalization;

namespace Chat.Infraestructura.Presencia;

/// <summary>
/// Identidad de esta instancia del servidor dentro del clúster.
/// </summary>
/// <remarks>
/// <para>
/// Sirve para dos cosas: atribuir cada conexión a la réplica que la atiende —para poder
/// limpiar lo que deja atrás si se cae— y etiquetar la telemetría, de modo que en Seq y
/// en Jaeger se distinga qué instancia atendió cada petición.
/// </para>
/// <para>
/// Se toma del entorno si el despliegue lo indica y, si no, del nombre de la máquina,
/// que dentro de un contenedor es su identificador. Se le añade un sufijo aleatorio
/// para que dos arranques del mismo contenedor no compartan identidad: si la
/// compartieran, el nuevo proceso heredaría las conexiones fantasma del anterior.
/// </para>
/// </remarks>
public sealed class IdentidadReplica
{
    /// <summary>Variable de entorno con la que el despliegue puede fijar el nombre.</summary>
    public const string VariableEntorno = "DOTCHAT_REPLICA";

    /// <summary>Crea la identidad resolviendo el nombre y el identificador únicos.</summary>
    public IdentidadReplica()
    {
        Nombre = Environment.GetEnvironmentVariable(VariableEntorno) is { Length: > 0 } configurado
            ? configurado
            : Environment.MachineName;

        var arranque = Environment.TickCount64.ToString("x", CultureInfo.InvariantCulture);
        Id = $"{Nombre}-{arranque}-{Environment.ProcessId}";
    }

    /// <summary>Nombre legible de la réplica, estable entre reinicios.</summary>
    public string Nombre { get; }

    /// <summary>Identificador único de esta ejecución concreta.</summary>
    public string Id { get; }
}
