namespace Advance.Data;

public static class Scenario
{
    public static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.FromHours(2));
    public static readonly DateTimeOffset Deadline = Now.AddDays(1);

    public const string OrderQuestion =
        "L'ordine ORD-1042 doveva arrivare oggi. Domani parto per Torino: " +
        "quali alternative ho? Non confermare nulla senza chiedermelo.";

    public const string Context =
        "Rispondi in italiano, brevemente. Sono dati sintetici di una demo. " +
        "Oggi e' il 5 ottobre 2026 alle 09:00 UTC+02:00. " +
        "La scadenza e' il 6 ottobre 2026 alle 09:00 UTC+02:00. " +
        "Usa solo i fatti forniti o letti dai tool; non inventare dati. ";

    public const string DeliveryProcedure =
        "Leggi ordine e alternative usando entrambi i tool. " +
        "Escludi opzioni oltre la scadenza, proponi prima quelle gratuite. " +
        "Indica costi, orari e vincoli. Chiedi se il cliente puo' raggiungere il deposito. " +
        "Puoi solo leggere e suggerire: non prenotare e non dichiarare azioni eseguite.";

    public static Order? FindOrder(string id) => id switch
    {
        "ORD-1041" => new(id, "regolare", Now.AddHours(9), Now.AddHours(9), null),
        "ORD-1042" => new(id, "in ritardo", Now.AddHours(9), Now.AddDays(2).AddHours(3), "Ritardo nel trasporto"),
        "ORD-1043" => new(id, "in ritardo", Now.AddHours(9), Now.AddDays(2).AddHours(9), "Nessuna alternativa disponibile"),
        _ => null
    };

    public static DeliveryOption[] Options(string orderId) => orderId == "ORD-1042"
        ? [
            new("OPT-1042-DEPOT", "Ritiro al deposito", Now.AddHours(5), Now.AddHours(9),
                0m, "EUR", "Documento necessario; verificare che il cliente possa raggiungere il deposito."),
            new("OPT-1042-POINT", "Punto di ritiro", Now.AddDays(1).AddHours(7), Now.AddDays(1).AddHours(9),
                4m, "EUR", "Disponibile solo dalle 16:00 del 6 ottobre: oltre la scadenza.")
          ]
        : [];
}

public sealed record Order(string OrderId, string Status, DateTimeOffset PromisedDelivery,
    DateTimeOffset EstimatedDelivery, string? DelayReason);

public sealed record DeliveryOption(string OptionId, string Type, DateTimeOffset AvailableFrom,
    DateTimeOffset AvailableUntil, decimal Cost, string Currency, string Requirements);
