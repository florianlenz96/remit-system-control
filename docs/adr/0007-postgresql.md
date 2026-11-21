# ADR 0007: PostgreSQL für Event Store und Tabellen

- Status: vorgeschlagen
- Datum: 2026-11-21

## Kontext

ADR 0004 verlangt einen append-only Event Store und Tabellen für den aktuellen Stand.
Im PoC soll möglichst wenig Infrastruktur betrieben werden.

## Entscheidung (Vorschlag)

Azure Database for PostgreSQL – Flexible Server für beides: eine Event-Tabelle (append-only,
pro Aggregat fortlaufende Version) und normale Tabellen als Projektionen.

## Konsequenzen

- Eine Datenbank, eine Transaktion für Event und Projektion möglich.
- Bekanntes, gut unterstütztes System, auch im Kundentenant einfach auszurollen.
- Bei sehr hohem Event-Volumen später ggf. ein spezialisierter Store – für den Verlauf der Zentrale nicht zu erwarten.

## Verworfene Alternativen

- **Cosmos DB mit Change Feed**: gut für Event Sourcing, aber zweites Datenbankmodell neben relationalen Projektionen.
- **Spezialisierter Event Store (z. B. EventStoreDB)**: zusätzliche Komponente ohne Not im PoC.
