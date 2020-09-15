# Uživatelská část
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

Pohyb pomocí šipek. Výstřel ze zbraně mezerníkem.




## Zbraně

### Pistole
Základní zbraň, umožňuje vystřelit 1 náboj každou vteřinu.

### Brokovnice
Vystřeluje 3 náboje, přebíjí se 2 vteřiny.

### Kulomet
Vystřeluje 1 náboj 6x za vteřinu. 




# Programátorská část

## Program
Položky s předponou bw- pojmenovávají `BackgroundWorkera`.
Příkazy s předponou Host- jsou posílány hostem.  Stavy s předponou Host- jsou stavy hosta. Totéž platí pro klienta a názvy s předponou Client-.



### Síťování

Pro odesílání a přijímání příkazů sloužících pro připojení klientů k hostovi je použit `TcpClient` a `TcpListener`, aby nedošlo k narušení připojovacího algoritmu ztrátou některých datagramů.

Broadcast a herní příkazy jsou posílány a přijímány přes `UdpClienta`.



#### HostConnecter

##### BCreateGame
Tlačítko "Vytvořit Hru". Přejde do stavu `HostSelect` ve kterém se nakonfiguruje počet hráčů ve hře a port, na kterém host poslouchá.



##### BCreateGame2
Tlačítko "Vytvořit". Spouští paralelního `bwClientWaitera`.



##### bwClientWaiter
Spouští paralelního `bwBroadcastRespondera`.
Dále připojuje klienty do hry posláním příkazu `HostLoginAccepted`.

Po připojení dostatečného množství klientů pošle host všem klientům příkaz `HostMoveToWaitingRoom` a přepne se do stavu `HostWaitingRoom`, odkud je možné spustit hru tlačítkem `BStartGame`.



##### bwBroadcastResponder
Odpovídá na `ClientFindServers` broadcast, tím dá klientovi vědět, že host existuje.



##### BStartGame
Pošle všem klientům příkaz `HostStartGame`. Vytvoří instanci `Player` pro každého klienta i pro sebe. Spouští tikání `TimerInGame`. Vytváří instanci `HostInGame`.



#### ClientConnecter
##### BConnect
Tlačítko "Připojit se". Přepne klienta do stavu `ClientSearch`, odkud je možné vyhledávat servery na zvoleném portu.



##### BRefreshServers
Tlačítko "Vyhledat servery". Spouští paralelního `bwServerRefresher`.



##### bwServerRefresher
Broadcastuje `ClientFindServers`, aby objevil všechny hostitele, poslouchající na zvoleném portu.



##### BLogin
Tlačítko "Připojit". Odešle `ClientLogin` na zvolený server, po přijetí příkazu `HostLoginAccepted` se přepne do stavu `ClientWaiting` a spustí paralelního `bwHostWaiter`.



##### bwHostWaiter
Čeká na příkazy `HostMoveToWaitingRoom` a `HostStartGame`.

Po přijetí `HostMoveToWaitingRoom` změní stav na `ClientWaitingRoom`. 

Po přijetí `HostStartGame` spouští `InGameClienta`.




#### HostInGame
Po vytvoření spustí paralelní `bwGameListener`.



##### bwGameListener
Zpracovává klientovy pohyby a výstřely ze zbraně a mění podle nich stav hry. Po uplynutí nastaveného času ukončí hru příkazem `HostGameEnded`.



##### TimerInGame
Snižuje aktuální herní čas, broadcastuje příkaz `HostTick`. Dále zajišťuje pohyb herních objektů a jejich konstrukci a destrukci.



#### ClientInGame
Po vytvoření spustí paralelní `bwGameListener`.



##### bwGameListener 
Zpracovává příkazy hosta vygenerované tiknutím `TimerInGame` a na základě těchto příkazů upravuje souřadnice a existenci objektů ve hře.

S každým přijatým příkazem `HostTick` se volá `Invalidate` a tím dochází k překreslení hry a k odeslání informací `playerMovement` a `weaponState` hostiteli. Samotné překreslování hry se provádí přes událost `FormBambulanci_Paint`.

Po přijetí příkazu `HostGameEnded` dochází k ukončení `bwGameListenera` a k zobrazení skóre.



### Hra

####  WeaponBox
Box obsahující `Weapon` může být sebrán instancí `Player`, kterému je po zavolání `ChangeWeapon `vyměněna zbraň.



#### Weapon
Abstraktní třída, umožňuje střílení. Jejími potomky jsou `Pistol`, `Shotgun`, `Machinegun`.



#### MovableObject
Abstraktní třída pro `Player` a `Projectile`, která umožňuje pohyb objektů metodou `Move`.

Při pohybu dochází k detekci kolize se zdmi: `DetectWalls`, s hráči: `DetectPlayers` či s boxy: `CollectBoxes`. Při kolizi střely s hráčem dojde k jeho úmrtí: `Die`. Při kolizi střely se zdí dojde k její destrukci: `DestroyIfDestructable`. 



#### Projectile
Potomek `MovableObjectu`. Obsahuje informace o střelách. Všechny vystřelené projektily jsou uloženy v Listu pod instancí `Game`.



#### Player
Potomek `MovableObjectu`. Obsahuje informace o klientově hráči. Každý hráč je uložen pod instancí `Game` buď jako živý v Listu `Players` nebo mrtvý v Listu `DeadPlayers`.



#### Map
Generuje pozadí hry složené z jednotlivých tile bloků. Prozatím existuje jen jedna mapa generovaná pomocí `GetStandardMap`.

Metoda `IsWall` detekuje zdi na základě `wallTiles`.



#### GraphicsDrawer

Vytváří a ukládá si bitmapy pro objekty.

Umožňuje vykreslovat objekty na obrazovku.



#### Game
Obsahuje informace o všech herních objektech.

Metoda `GetSpawnCoords` generuje souřadnice pro nové objekty.  



## Co nebylo doděláno

* Hráči po smrti zůstává jeho aktuální zbraň. Ta by se vždy při jeho oživení mohla vyresetovat zpět na pistoli.

* Hostitel se všemi svými klienty komunikuje broadcastem přes UdpClienta na statickém portu. To však znemožňuje rozumný běh více herních serverů. Tento problém by šel řešit například využíváním multicast group místo broadcastu.

* Ve stavu HostSelect by mohla být konfigurace rozšířena o volbu mapy nebo herního módu.

* Ve stavu ClientWaitingRoom by se stavu mohlo nastavit například jméno hráče a jeho barva.

* Při vytváření instance Game by se mohl předávat delegát na generátor mapy, pro umožnění výběru z více druhů map.

* Není ošetřen případ, kdy se klient po připojení serveru odpojí.

* Pokud host zruší hostování, připojení klienti se to nedozví.




## Chyby

* Z důvodu statického portu pro klienty není možné spustit na jednom počítači více klientských aplikací.