# 01 — Viziune și domeniu

## Scop

WorkNotes este un caiet de lucru pentru munca de zi cu zi pe proiecte: jurnalul cronologic al zilei și fișe de lucru (articole) despre subiecte precise — de exemplu un CR, un bug sau o livrare. Captura trebuie să fie rapidă (o notă se creează direct pe tablă, titlul este opțional), iar textul trebuie să poată fi regăsit ulterior după context și, în etapele următoare, după referințele de lucru (CR-uri, buguri).

## Concepte

| Concept | Sens | Stare |
| --- | --- | --- |
| **Context** | Spațiul de lucru în care stau notele: un client sau o firmă (de exemplu SD Worx, TopDev). Orice notă aparține unui singur context. | realizat (`WorkContexts`) |
| **Membru al contextului** | Utilizator cu acces la un context, cu rolul `Owner` sau `Member`. Creatorul contextului este `Owner`. | realizat (`ContextMembers`) |
| **Notă** | Unitatea de lucru, de tip **jurnal** (legat de o zi, `JournalDate`) sau **articol** (fișă tematică, fără dată). Are titlu opțional, vizibilitate și audit. | realizat (`Notes`) |
| **Paragraf (bloc)** | Textul dintre rânduri goale din editor. Are identitate stabilă și audit propriu (creat / modificat), independent de rândurile vizuale. | realizat (`NoteBlocks`) |
| **Tabla** | Afișarea notelor unui context ca post-it-uri, grupate pe luni după ultima modificare. | realizat |
| **Referință de lucru** | CR sau bug dintr-un sistem extern, catalogat o singură dată pe context și asociat notelor și paragrafelor. | planificat (`WorkReferences`) |
| **Legătură** | URL extern atașat unei note sau unui paragraf. | planificat (`NoteLinks`) |

Planul inițial numea tipul fișei „Document”; în aplicație tipul se numește **Article** (articol).

## Exemplu de utilizare (din planul inițial)

```text
Context: SD Worx
 ├─ WorkReference: CR 30042                      (planificat)
 ├─ Notă: jurnalul unei zile
 │   └─ Paragraf: „Am verificat scripturile…”    → asociat cu CR 30042 (planificat)
 └─ Notă: articolul „CR 30042”
     ├─ Paragraf: analiză
     ├─ Paragraf: SQL pentru versiunea 2.2.13.4
     └─ Paragraf: particularități custom
```

Jurnalul păstrează cronologia, iar articolul adună explicațiile; codul CR-ului leagă cele două. Un eveniment real (de exemplu „Publish efectuat”) va avea propria înregistrare în modulul de livrări: simpla lui menționare într-un paragraf nu confirmă nimic.

## Principii

- **Contextul este granița de acces.** Fiecare citire și modificare verifică apartenența la context; un context străin răspunde „nu există” (404), fără a-i confirma existența.
- **Notele sunt private implicit.** O notă `Context` este vizibilă membrilor, dar editabilă numai de proprietar. Rolul de administrator nu dă acces la notele private.
- **Captura nu se blochează.** Titlul este opțional; se pot crea oricâte jurnale pe zi.
- **Textul utilizatorului rămâne cum a fost scris.** Interfața este tradusă (ro/en/pl, prin `.resx`), conținutul nu.
- **Auditul este pe paragraf.** Redimensionarea ferestrei sau mutarea textului nu schimbă auditul; doar editarea textului îl actualizează.
- **Aplicația funcționează și fără JavaScript** pentru operațiile de bază; JavaScript adaugă confortul (editare pe loc, editor, taburi).
