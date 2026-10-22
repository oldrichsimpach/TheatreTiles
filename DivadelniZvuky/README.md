# TheatreTiles 0.2.1

Offline zvukové pokyny pro Windows. Spusťte TheatreTiles.exe ze složky se všemi přiloženými soubory. Vyžaduje .NET Desktop Runtime 10 (součást nainstalovaného SDK).

## Dlaždice a zvuk

- Levé kliknutí na dlaždici spustí zvuk od začátku. Hraje vždy jeden zvuk; další dlaždice ho nahradí. Červená znamená použitý pokyn.
- Pravé tlačítko: nahrát / změnit zvuk, upravit popisek, označit jako nepřehraný, **Fade → Vypnuto / 3 s / 5 s / 7 s**, hlasitost zvuku a **Odstranit dlaždici**.
- Fade běží na konci stopy od 100 % její nastavené hlasitosti k nule. U klipu kratšího než preset trvá celý klip.
- Šest bílých teček ve dvou řadách se objeví při najetí. Mají 30% opacity a slouží k přesouvání. Obrys ukazuje místo vložení a sousedé se animovaně rozestupují. Zrušení přesunu zachová pořadí.
- Dlaždice tvoří jednu vodorovnou řadu zleva doprava. Dlaždice + je poslední položkou a účastní se stejného řazení; po kliknutí její místo převezme nová zvuková dlaždice a + pokračuje na další pozici. Posouvání je **vodorovné**, pomocí kolečka nebo spodního posuvníku.
- Dole: časová osa s přímým kliknutím, pauza/přehrát, stop, ±5 s. Esc zastaví zvuk. Vpravo je hlavní hlasitost a ztlumení.

## Sekce

- Pravé tlačítko mimo sekci, také na okolním pozadí: **Přidat sekci**.
- Pravé tlačítko na prázdnou část sekce: **Přejmenovat / Duplikovat / Odstranit sekci**. V prázdné části sekce je také prostor nahoře a mezi dlaždicemi.
- Název vlevo nahoře je šedý s 30% opacity. Úchyt nahoře uprostřed tvoří šest šedých teček s 30% opacity. Za něj lze sekci přesunout do volného prostoru; sekce nepřesáhne pracovní plochu a nepřekryje sousedy.
- Rohy mění velikost. Na plné ploše se sousedé navzájem vytlačují přes společné hranice; při pohybu se okraje měkce deformují a po puštění pružně dosednou se zavlněním. Nikdy se nepřekrývají. Menší sekce zůstávají použitelné díky vodorovnému scrollu.
- Duplikování kopíruje popisky, zvuky, barvy i všechna nastavení dlaždic do nezávislých modelů.
- Pokud se nová kopie nevejde, aplikace přeuspořádá plochu: 2 sekce jsou nad sebou, 3 jsou dvě nad sebou vlevo a jedna přes celou pravou polovinu, 4 tvoří čtvrtiny. Další počty využijí pravidelnou mřížku.

## Zámek a historie

- Zámek vlevo od fullscreenu blokuje změny rozložení: přesouvání dlaždic, přesouvání a velikost sekcí i přidávání a odstraňování. Přehrávání, popisky a zvuková nastavení zůstávají dostupné. Stav se ukládá do projektu.
- **Ctrl+Z** vrací změny rozložení: pořadí dlaždic, přesuny a velikosti sekcí, přidání, kopírování a odstranění dlaždic/sekcí. Jedno tažení je jeden krok. Přehrávání ani nastavení existujících zvuků se zpět nevrací. Historie je pro aktuální otevřený projekt a neukládá se na disk.

## Projekty a fullscreen

**Uložit / Ctrl+S** uloží jediný soubor `.divz` včetně zvuků, sekcí, názvů, pořadí, rozložení, zámku, hlasitostí a fade presetů. Otevřít slouží k načtení. Starší projekty se automaticky převedou na jednu sekci. Nové projekty verze 2 otevírejte v TheatreTiles 0.2.1 nebo novějším; starší verze programu je nepřečtou. Pro dvojklik přiřaďte `.divz` k novému TheatreTiles.exe přes Windows Otevřít v programu → Vždy.

F11 nebo šipky vpravo nahoře přepínají fullscreen přes celý monitor včetně hlavního panelu Windows. Druhé stisknutí obnoví původní okno. Titulková lišta je černá na podporovaných Windows.

Import: MP3, WAV, WMA, AIFF, M4A, AAC, FLAC. Přehrávání konkrétního kódování závisí na Windows. Automaticky ověřen PCM WAV; vlastní zvuky a slyšitelný výstup vyzkoušejte na cílovém počítači.

## Vývoj

Sestavení: `dotnet build DivadelniZvuky/DivadelniZvuky.csproj -c Release`.
Integrační ověření: `TheatreTiles.exe --self-test C:\absolutni\slozka`.
## Novinky 0.2.1 — kontakt sekcí

Při tlačení do sousední sekce používají obě stěny jednu společnou pružnou křivku se 41 uzly. Lokální tlak se šíří mezi uzly, průhyb a návrat řídí pružnost a tlumení. Křivka se používá i pro skutečné ořezání obsahu obou sekcí, takže se jejich viditelné plochy nepřekrývají. Při puštění před prahem se stěna uvolní a rozložení zůstane stejné.

Po dostatečném zatažení (podle velikosti sousední sekce) se změní hierarchie rozložení. Horní levá čtvrtina tlačená doprava získá celou horní polovinu; původní pravá polovina se stáhne do pravé dolní čtvrtiny vedle původní levé dolní sekce. Zrcadlově funguje roztažení dolní levé čtvrtiny přes spodní polovinu. Nejprve se vytlačená sekce stáhne a uvolní místo, teprve potom do něj tlačící sekce doroste, aby během přesunu nedošlo k překrytí.

Zavlnění jako po doskočení se spouští až po dosednutí a pouze u přímo vytlačené sekce. Tlačící sekce tento efekt nemá. Přesun se vrací jedním Ctrl+Z; zámek chování blokuje. Dvě nové automaticky rozložené sekce jsou horizontální poloviny (horní a dolní), existující uložená rozložení se zachovají.

Integrační test ověřuje společnou křivku a průnik geometrií, přeskupení horní i dolní čtvrtiny, správné načasování a výběr sekce pro rebound, zrušení tlaku, historii, rozložení dvou sekcí a řazení dlaždic s inline +.
