# TheatreTiles

<img src="DivadelniZvuky/Assets/TheatreTiles.png" width="112" alt="Ikona TheatreTiles">

**Jednoduchá aplikace pro zvukaře v divadle.** TheatreTiles pomáhá připravit zvukové pokyny pro představení a spouštět je přes velké, přehledně popsané dlaždice. Cílem je rychlé ovládání a minimum rušivých prvků.

Aktuální verze: **0.2.1** · Platforma: **Windows** · Technologie: **C# / .NET 10 / WPF**.

## Co aplikace umí

- **Zvukové dlaždice:** vlastní popisek, import zvuku a přehrání od začátku jedním kliknutím. Zelená označuje nepoužitý pokyn, červená již použitý. Hraje vždy jeden zvuk.
- **Pojmenované sekce:** organizace například podle dějství, přesouvání, změna velikosti, duplikování včetně zvuků a nastavení i odstranění.
- **Vodorovné řazení:** dlaždice jsou v jedné řadě s vodorovným posouváním. Poslední dlaždice „+“ přidává další pokyn.
- **Ovládání přehrávání:** časová osa se skokem jedním kliknutím, pauza, stop a posun o ±5 sekund.
- **Hlasitost:** hlavní hlasitost, ztlumení a samostatné nastavení každého zvuku.
- **Fade-out:** vypnuto nebo plynulé zeslabení posledních 3, 5 či 7 sekund do ticha.
- **Práce s rozložením:** živý náhled přesouvání dlaždic, zámek rozložení a Ctrl+Z pro vrácení změn.
- **Pružné sekce:** kontaktní stěny se při tlačení lokálně deformují. Po překročení prahu se rozložení přeskupí bez překrývání a vytlačená sekce po dosednutí lehce zavlní.
- **Přenosné projekty:** jediný soubor `.divz` obsahuje zvuky, popisky, sekce, jejich rozložení, pořadí, hlasitosti, fade presety i stav zámku. Původní zvukové soubory už po uložení nejsou potřeba.
- **Fullscreen:** F11 nebo tlačítko vpravo nahoře; zobrazení pokrývá celý monitor včetně oblasti hlavního panelu Windows.

## Základní ovládání

| Akce | Ovládání |
| --- | --- |
| Spustit zvuk / spustit znovu od začátku | Levé kliknutí na dlaždici |
| Nastavení, import, popisek, fade a odstranění | Pravé kliknutí na dlaždici |
| Změnit pořadí dlaždic | Tažení za šest teček při najetí |
| Přidat sekci | Pravé kliknutí mimo sekci |
| Přejmenovat, duplikovat nebo odstranit sekci | Pravé kliknutí na prázdnou část sekce |
| Přesunout sekci / změnit velikost | Horní úchyt / rohy sekce |
| Uložit projekt | Ctrl+S |
| Vrátit změnu rozložení | Ctrl+Z |
| Zastavit přehrávání | Esc |
| Přepnout fullscreen | F11 |

Dvě automaticky uspořádané sekce tvoří horní a dolní polovinu. Tři sekce tvoří dvě čtvrtiny vlevo a jednu polovinu vpravo; čtyři sekce tvoří čtvrtiny.

## Zvukové formáty

Import podporuje **MP3, WAV, WMA, AIFF, M4A, AAC a FLAC**. Přehrávání konkrétního kódování závisí na kodecích Windows. Nejde o multitrackový mixer: současně se přehrává jeden zvuk.

## Sestavení ze zdrojového kódu

Potřebujete Windows a **.NET 10 SDK**.

```powershell
dotnet build DivadelniZvuky/DivadelniZvuky.csproj -c Release
dotnet run --project DivadelniZvuky/DivadelniZvuky.csproj -c Release
```

Vytvoření spustitelné složky:

```powershell
dotnet publish DivadelniZvuky/DivadelniZvuky.csproj -c Release --self-contained false -o dist/TheatreTiles
```

Takto sestavená aplikace vyžaduje na cílovém počítači .NET Desktop Runtime 10. Při přenosu zachovejte celou publikovanou složku.

## Stav projektu a ověření

TheatreTiles je vyvíjený prototyp. Integrační ověření pokrývá ukládání zvuků do projektů, sekce, historii, zámek, fade presety, přehrávání PCM WAV, horizontální posouvání, fullscreen i geometrii kontaktů sekcí.

Test se spouští takto; výstupní složka je určena pro testovací projekt, výsledky a obrázky rozhraní:

```powershell
TheatreTiles.exe --self-test C:\TheatreTiles-test
```

Před skutečným představením vyzkoušejte vlastní zvuky a zvukový výstup na cílovém počítači. Historie Ctrl+Z se neukládá mezi spuštěními a projekt se ukládá ručně.

Podrobný návod najdete v [dokumentaci aplikace](DivadelniZvuky/README.md).

## Obsah repozitáře

- `DivadelniZvuky/` — zdrojový kód aplikace, ikona, návod a integrační testy. Název složky zachovává původní vývojový název; výsledná aplikace se jmenuje TheatreTiles.
- `NuGet.Config` — konfigurace sestavení bez externích balíčků.
- `.gitignore` — vynechává výstupy sestavení, lokální nastavení a uživatelské zvuky/projekty.

Repozitář slouží jako veřejná záloha zdrojového kódu a dokumentace. Neobsahuje zvuky konkrétních představení ani osobní projekty.

## Licence

TheatreTiles je dostupný pod [MIT licencí](LICENSE). Copyright (c) 2026 oldrichsimpach.
