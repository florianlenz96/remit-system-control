# Vision

## Die Idee in einem Satz

Programmiersprachen verlieren für die meisten Beteiligten an Bedeutung: Wer ein System verantwortet,
beschreibt künftig Absicht und Grenzen – KI baut und betreibt. Die Kommandozentrale ist der Ort,
an dem das passiert.

## Warum

- Die Bausteine gibt es einzeln (Coding-Agents, SRE Agent, Chaos Studio, Ticketsysteme, Wikis),
  aber nichts verbindet sie zu einem durchgehenden Kreislauf von der Absicht bis zum Betrieb.
- Kunden werden KI keinen Vollzugriff auf Produktivsysteme geben. Sie brauchen abgestuftes,
  messbares Vertrauen.
- Die Rolle „Technical Product Owner“ entsteht: jemand, der das Gesamtsystem versteht und in
  technischer Fachsprache beschreibt, was er will, ohne jede Komponente verstehen zu müssen.

## Die Rolle des TPO

- Formuliert oder übernimmt **Bedarfe** (z. B. aus Jira).
- Bewertet **Vorschläge** der KI und legt **Systemeigenschaften** fest: Ziele, TTL, Backup,
  Ausfallverhalten, Budget, Datenstandort.
- Legt pro **Bereich** fest, wie viel die KI selbst darf (**Autonomie**) und wie genau sie
  erklären muss (**Transparenz**).
- Trifft die Entscheidungen, die keine Technik beantworten kann – vor allem, wenn Eigenschaften
  kollidieren (Beispiel: zum Black Friday sind Ziel-Antwortzeit und Budget nicht beide haltbar).
- Behält die **Ownership**. Die KI führt aus.

## Der Kreislauf

1. **Bedarf**: „Der Shop soll schneller antworten.“
2. **Vorschlag**: Die KI analysiert die Ursache und bietet Optionen an (Cache, CDN, DB skalieren).
3. **Eigenschaften**: Der TPO nimmt eine Option an und legt fest, was gelten muss.
4. **Bauen**: Ein Bau-Agent setzt um und dokumentiert jede eigene Entscheidung mit Begründung.
5. **Betreiben**: Ein Betriebs-Agent hält die Eigenschaften dauerhaft ein.
6. **Lernen**: Abweichungen führen zu neuen Entscheidungen – durch die KI oder als Rückfrage.

## Autonomie und Transparenz

Wie viel der TPO verstehen muss, hängt nicht von der technischen Komplexität ab, sondern davon,
was im schlimmsten Fall passiert. Drei Fragen pro Bereich:

- Ist der Schaden begrenzt?
- Lässt sich ein Fehler rückgängig machen?
- Sind Menschen oder Recht betroffen?

Ein Cache (begrenzt, umkehrbar, rekonstruierbar) bekommt hohe Autonomie und wenig Transparenz.
Kundendaten (nicht umkehrbar, DSGVO-Nachweispflicht) bekommen niedrige Autonomie und volle
Transparenz bis hin zum Datenfluss.

**Verdiente Autonomie:** Die Zentrale bewertet KI-Entscheidungen im Nachhinein. Ab einer
einstellbaren Erfolgsquote schlägt sie dem TPO eine höhere Stufe vor – entscheiden muss er selbst.

Die Autonomie wird zusätzlich über Azure-Rechte durchgesetzt: Ein Agent auf Stufe 1 hat nur
Leserechte und *kann* nichts ändern. Autonomie ist damit technisch garantiert, nicht nur vereinbart.

## Auslieferung

Die Zentrale läuft im **Azure-Tenant des Kunden**, eine Instanz pro Kunde, ausgerollt per Bicep,
später als Azure Managed Application. Daten, Modelle (Microsoft Foundry, EU-Region) und Agents
bleiben beim Kunden.

## Geschäftsmodell

- Framework und Methode als Marketing und Positionierung („CAF für das KI-Zeitalter“ –
  Ergänzung, keine Konkurrenz zu Microsoft).
- Lizenz pro Instanz.
- Beratung: Assessment („Sie stehen bei Stufe 1“), Einführung, Begleitung beim Aufstieg
  der Autonomiestufen.

## Abgrenzung

Backstage, Port, Cortex sind Kataloge – sie zeigen, was existiert. Die Kommandozentrale
*steuert* den Kreislauf von Absicht zu Betrieb.
