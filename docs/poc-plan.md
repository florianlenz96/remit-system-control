# PoC-Plan: Cache-Szenario

## Ziel

Ein Szenario echt und vollständig durchziehen – von Bedarf bis Betrieb, auf echtem Azure.
Hinter den Kulissen darf anfangs manches halb manuell sein; der Ablauf für den TPO muss echt sein.

**Szenario:** „Schnellere Antwortzeiten im Online-Shop“ → Cache für Katalog und Suche
(Details: [`datenmodell.md`](datenmodell.md#beispiel-cache-für-den-online-shop)).

## Umfang

Im PoC:

- Eine Instanz in der eigenen Azure-Subscription, ein Nutzer (der TPO)
- Ein Beispielsystem „Online-Shop“ (minimale Demo-App mit Katalog-API und Datenbank)
- Ein Bereich „Katalog und Suche“, Autonomie 3, Transparenz 1
- Bedarfe werden direkt in der Zentrale angelegt (kein Jira)
- Die drei Screens aus dem Klick-Entwurf: Bedarf, Eigenschaften, Betrieb

Nicht im PoC:

- Jira-, Confluence-Anbindung
- Mehrere Bereiche, Transparenzstufe 3 (Datenfluss-Ansicht)
- Verdiente Autonomie
- Auslieferung als Managed Application

## Schritte

0. **Tech-Stack entscheiden** (ADR 0008) – mit dem Nutzer, nicht allein.
1. **Domänenkern**: Entitäten, Events, Projektionen; Regeln für Eskalation und Konflikte mit Tests.
2. **Zentrale**: Web-App mit den drei Screens auf Basis des Klick-Entwurfs; Anmeldung über Entra ID.
3. **MCP-Server der Zentrale**: Eigenschaften und Richtlinien lesen, Entscheidungen und Rückfragen melden.
4. **Infrastruktur**: Bicep für Zentrale (Container Apps, PostgreSQL, Managed Identity) und Demo-Shop.
5. **Bau-Agent**: Angenommene Option als Auftrag an den GitHub Copilot Coding Agent; Ergebnis (PR, Deployment) als Verweis zurück.
6. **Messung**: Application Insights und Cost Management lesen; Soll-Ist auf dem Betriebs-Screen.
7. **Betriebs-Agent**: SRE Agent anbinden; prüfen, ob Freigaben im Review-Modus über Agent Hooks in der Zentrale landen.
8. **Ernstfall**: Lasttest erzeugt eine Kollision Ziel ↔ Budget → Rückfrage erscheint beim TPO.

## Abnahmekriterien

- [ ] Der TPO legt einen Bedarf an und erhält einen Vorschlag mit mindestens zwei Optionen.
- [ ] Beim Annehmen fragt die Zentrale genau die `benötigteEigenschaften` der Option ab.
- [ ] Der Cache wird ohne manuellen Eingriff im Portal gebaut; jede KI-Entscheidung steht mit Begründung im Protokoll.
- [ ] Der Betriebs-Screen zeigt Soll und Ist aller Eigenschaften aus echten Messwerten.
- [ ] Eine Kollision zweier „muss“-Eigenschaften erzeugt eine Rückfrage mit Optionen, Frist und Standardoption.
- [ ] Der TPO sieht an keiner Stelle Code, Git oder Ressourcen-IDs.
- [ ] Die gesamte Infrastruktur lässt sich per Bicep in eine leere Subscription ausrollen.

## Parallel

Ab Woche 3 Klick-Entwurf und Zwischenstand zwei, drei vertrauten Kunden oder Kollegen zeigen.
Besonders beobachten: Reaktion auf die Autonomiestufen.
