# Uživatelská dokumentace
Hra Bambulánci je 2D střílečka pro 2 až 4 hráče připojené po síti. Bambulánci jsou inspirováni hrou Bulánci.

Ke spuštění hry je potřeba 1 hostitel. Ostatní, nehostující hráči se připojují k hostiteli.

Po spuštění hry se načte mapa a jednotliví hráči.

Úkolem každého hráče je zabít protivníka a přitom co možná nejméně umírat. K tomu mu mohou pomoci bedny, ve kterých se nachází zbraně. V bedně je obsažena pistole, kulomet či brokovnice.

Po uplynutí nastaveného času dojde k ukončení hry a výpisu počtu zabití a úmrtí jednotlivých hráčů.



## Hostitel

Vytváří hru. Při vytváření hry je možné nastavit délku hry, počet hráčů a port, na kterém bude hostitel poslouchat.



## Klient

Připojuje se k hostiteli přes port, na kterém hostitel poslouchá.



## Ovládání

Pohyb pomocí šipek. Střílení ze zbraně mezerníkem.



## Zbraně
### Pistole
Základní zbraň, umožňuje vystřelit 1 náboj každou vteřinu.

### Brokovnice
Vystřeluje 3 náboje, přebíjí se 2 vteřiny.

### Kulomet
Vystřeluje 1 náboj 6x za vteřinu. 



# Programátorská dokumentace

--Timer mozna neni dost konkretni

--zkontrolovat flow backgroundWorkeru

## Program

### Síťování

Většina příkazů pro připojování klientů k hostiteli se posílá přes protokol TCP, aby nedošlo k narušení jednotlivých kroků připojování. Broadcast a samotný běh hry komunikuje pomocí protokolu UDP.

#### HostConnecter

##### BCreateGame
Umožní konfiguraci hry.

##### BCreateGame2
Spouští paralelního `bwClientWaiter`.

##### bwClientWaiter
Odpovídá na `ClientFindServers` broadcast, tím dá klientovi vědět, že existuje. Dále připojuje klienty do hry. Po připojení dostatečného množství hráčů host pošle všem klientům příkaz `HostMoveToWaitingRoom` a přepne se do stavu `HostWaitingRoom`, odkud je možné spustit hru tlačítkem `BStartGame`. 

##### BStartGame
Pošle všem klientům `HostStartGame`. Vytvoří instanci `Player` pro každého klienta i pro sebe. Spouští `HostInGame`. Zapne `TimerInGame`.

#### ClientConnecter
##### BConnect
Přepne klienta do stavu `ClientSearch`, odkud je možné vyhledávat servery.

#####  BRefreshServers
Spouští paralelního `bwServerRefresher`.

##### bwServerRefresher
Broadcastuje `ClientFindServers`, aby objevil všechny hostitele, poslouchající na daném portu.

##### BLogin
Odešle `ClientLogin` na zvolený server, po přijetí se přepne do stavu `ClientWaiting` a spustí paralelního `bwHostWaiter`.

##### bwHostWaiter
Čeká na příkazy `HostMoveToWaitingRoom` a `HostStartGame`, ve kterém se nastartuje `InGameClient`.

#### HostInGame

Po vytvoření spustí paralelní `bwGameListener`.

##### bwGameListener
Zpracovává hráčovy aktivity a mění podle nich stav hry. Po uplynutí nastaveného času ukončí hru příkazem `HostGameEnded`.

##### TimerInGame
Snižuje aktuální herní čas, broadcastuje příkaz `HostTick`, přepočítává herní stav.



#### ClientInGame

Po vytvoření spustí paralelní bwGameListener.

##### bwGameListener
Zpracovává příkazy hosta a podle nich upravuje klientovy informace o hře. S každým přijatým příkazem HostTick dochází k překreslení hry a k odeslání informací playerMovement a weaponState hostiteli. Po ukončení bwGameListenera se zobrazí skóre.

Překreslování hry se provádí přes FormBambulanci_Paint - form paint event.



### Hra

#### WeaponBox
Je box obsahující zbraň, která je po sebrání boxu přidělena hráči.

#### Weapon
Je abstraktní třída, umožňuje střílení. Jejími potomky jsou `Pistol`, `Shotgun`, `Machinegun`.

#### MovableObject
Je abstraktní třída pro `Player` a `Projectile`, která umožňuje pohyb objektů metodou `Move`. Při pohybu dochází k detekci kolize se zdmi: `DetectWalls`, s hráči: `DetectPlayers` či s boxy: `CollectBoxes`. Při kolizi střely s hráčem dojde k jeho úmrtí. Při kolizi střely se zdí dojde k její destrukci: `DestroyIfDestructable`. 

#### Projectile
Obsahuje veškeré informace o střelách.

#### Player
Obsahuje veškeré informace o hráči.

#### Map
Generuje pozadí hry složené z jednotlivých tile bloků. Metoda `IsWall` detekuje zdi na základě wallTiles.

#### GraphicsDrawer
Umožňuje vykreslovat objekty na obrazovku. Vytváří bitmapy pro objekty. 

#### Game
Obsahuje informace o všech herních objektech. Metoda GetSpawnCoords generuje souřadnice pro nové objekty.  

### Konec hry

napsane pod hostInGame a clientInGame...--------------------------------------

## Prubeh prace

## Nedodelavky

## Chyby



