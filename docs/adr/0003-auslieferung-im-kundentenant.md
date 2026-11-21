# ADR 0003: Auslieferung in den Azure-Tenant des Kunden, eine Instanz pro Kunde

- Status: angenommen
- Datum: 2026-11-20

## Kontext

Optionen: lokale App, SaaS beim Anbieter, oder Betrieb im Tenant des Kunden.
Agents müssen rund um die Uhr nah an den Systemen laufen; mehrere Personen arbeiten zusammen;
der TPO ist kein Entwickler. Zielgruppe sind vor allem deutsche Enterprise-Kunden
(Datenschutz, Betriebsrat, Zugriff Externer auf Produktivsysteme).

## Entscheidung

Die Kommandozentrale ist eine Web-Anwendung und läuft **im Azure-Tenant des Kunden**.
Eine Instanz pro Kunde, ausgerollt per Bicep; später als Azure Managed Application.

- Anmeldung über die Entra ID des Kunden
- Agents mit Managed Identities und minimalen Rechten
- Modelle über Microsoft Foundry in einer EU-Region des Kunden
- Versionierte Releases; der Kunde entscheidet, wann er aktualisiert

## Konsequenzen

- Keine Mandantentrennung im Code nötig.
- Alles muss als Infrastructure as Code ausrollbar sein – von Anfang an, auch im PoC.
- Wir brauchen einen Update-Kanal und Support für viele getrennte Instanzen.
- Geschäftsmodell: Lizenz pro Instanz plus Einführung und Begleitung als Beratung.

## Verworfene Alternativen

- **Lokale App**: Betrieb rund um die Uhr und Zusammenarbeit nicht möglich.
- **SaaS beim Anbieter**: einfacher zu betreiben, aber für die Zielgruppe schwer verkäuflich.
