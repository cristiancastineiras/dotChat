using System.Globalization;
using System.Text;

namespace Chat.ClienteCli.Infraestructura;

/// <summary>
/// Divide una línea escrita en la consola interactiva en argumentos, respetando
/// las comillas simples y dobles igual que haría un intérprete de órdenes.
/// </summary>
public static class DivisorArgumentos
{
    /// <summary>Convierte una línea de texto en el vector de argumentos equivalente.</summary>
    /// <param name="linea">Línea tal cual la escribió el usuario.</param>
    /// <returns>Argumentos individuales, ya sin comillas.</returns>
    public static string[] Dividir(string? linea)
    {
        if (string.IsNullOrWhiteSpace(linea))
        {
            return [];
        }

        var argumentos = new List<string>();
        var actual = new StringBuilder();
        var hayToken = false;
        var comillaAbierta = '\0';

        foreach (var caracter in linea)
        {
            // Se descartan marcas de orden de bytes y demás caracteres invisibles:
            // aparecen al pegar texto o al redirigir la entrada, y romperían el
            // nombre de la orden sin que el usuario vea el motivo.
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.Format)
            {
                continue;
            }

            if (comillaAbierta != '\0')
            {
                // Dentro de comillas todo es literal hasta encontrar la de cierre.
                if (caracter == comillaAbierta)
                {
                    comillaAbierta = '\0';
                }
                else
                {
                    actual.Append(caracter);
                }

                continue;
            }

            if (caracter is '"' or '\'')
            {
                comillaAbierta = caracter;
                // Marca el token como iniciado para conservar cadenas vacías ("").
                hayToken = true;
                continue;
            }

            if (char.IsWhiteSpace(caracter))
            {
                if (hayToken)
                {
                    argumentos.Add(actual.ToString());
                    actual.Clear();
                    hayToken = false;
                }

                continue;
            }

            actual.Append(caracter);
            hayToken = true;
        }

        if (hayToken)
        {
            argumentos.Add(actual.ToString());
        }

        return [.. argumentos];
    }
}
