# XtreamApi — Guide d'utilisation

*[English version](usage-guide.md)*

Tout ce que la bibliothèque offre, et comment la mettre au travail dans une
application réelle. Ce guide suppose que vous avez lu l'avertissement du
`README`, et que le panel que vous interrogez est un panel auquel vous avez
légalement le droit d'accéder.

Pour le protocole lui-même — les actions brutes, leurs paramètres et leurs
particularités — voir [`xtream-api-reference.md`](xtream-api-reference.md). Ce
document-ci traite de la surface C#.

---

## Sommaire

1. [Portée de la bibliothèque](#1-portée-de-la-bibliothèque)
2. [Prérequis et installation](#2-prérequis-et-installation)
3. [Les quatre briques](#3-les-quatre-briques)
4. [Première connexion](#4-première-connexion)
5. [Référence des fonctionnalités](#5-référence-des-fonctionnalités)
6. [Construction des adresses de lecture](#6-construction-des-adresses-de-lecture)
7. [Configuration](#7-configuration)
8. [Suivi de l'avancement](#8-suivi-de-lavancement)
9. [Gestion des erreurs](#9-gestion-des-erreurs)
10. [Schémas d'intégration](#10-schémas-dintégration)
11. [Durée de vie, threads et annulation](#11-durée-de-vie-threads-et-annulation)
12. [Tolérance aux formats](#12-tolérance-aux-formats)
13. [Étendre la bibliothèque](#13-étendre-la-bibliothèque)
14. [Dépannage](#14-dépannage)

---

## 1. Portée de la bibliothèque

XtreamApi est un **client de protocole**. Il parle l'API des panels Xtream
Codes en HTTP, désérialise les réponses en objets typés, et assemble les
adresses dont un lecteur multimédia a besoin.

**Ce qu'il fait**

- Il authentifie un compte auprès d'un panel et rapporte son état.
- Il lit le catalogue : catégories, chaînes en direct, films, séries, épisodes.
- Il lit le guide électronique des programmes, y compris les fenêtres de
  rattrapage.
- Il ouvre le guide XMLTV et la playlist M3U sous forme de flux.
- Il construit les adresses de lecture des chaînes, des films, des épisodes et
  des rediffusions.

**Ce qu'il ne fait délibérément pas**

- Il **ne livre aucun contenu, aucune liste de chaînes, aucune adresse de
  serveur et aucun identifiant.** Tout cela vient de vous, à l'exécution.
- Il **ne découvre aucun panel**, ne sonde aucune adresse et n'embarque aucun
  annuaire de fournisseurs.
- Il **n'écrit jamais de média sur le disque.** La bibliothèque lit l'API et
  produit des adresses, rien de plus. Si une application consommatrice
  sauvegarde un flux — l'enregistrement personnel d'un programme que vous avez
  le droit de regarder — c'est sa fonctionnalité à elle, son code à elle, et sa
  responsabilité à elle.
- Il **ne contourne aucune protection.** Aucune gestion de DRM, aucun jeton
  forgé, aucune évasion de limitation de débit. Le seul en-tête qu'il vous
  laisse choisir est le `User-Agent`, parce que beaucoup de panels rejettent
  les requêtes qui n'en portent pas.

Gardez cette frontière en tête quand vous bâtissez par-dessus : le travail de
la bibliothèque s'arrête à *« voici la réponse typée, et voici l'adresse »*.

---

## 2. Prérequis et installation

| | |
| --- | --- |
| Cibles | `net8.0`, `net10.0` |
| Dépendances | aucune — uniquement `System.Text.Json` et `System.Net.Http` |
| Langage | C# `latest`, types référence nullables activés |

```bash
dotnet add package XtreamApi
```

Ou, depuis les sources, en référençant directement le projet :

```xml
<ProjectReference Include="..\XtreamApi.NET\src\XtreamApi\XtreamApi.csproj" />
```

`net8.0` est le plancher, volontairement : une application sur .NET 8, 9, 10 ou
au-delà peut consommer le paquet. La cible `net10.0` existe pour que le paquet
annonce explicitement le support de la version courante.

---

## 3. Les quatre briques

```
XtreamCredentials ──► XtreamClient ──► XtreamStreamUrlBuilder
                           │
                           └──► IXtreamTransport (politique HTTP, avancement)
```

**`XtreamCredentials`** — l'adresse du panel, un nom d'utilisateur et un mot de
passe. Immuable, validé à la construction, et son `ToString()` masque le mot de
passe pour qu'il puisse atteindre un journal sans danger.

**`IXtreamTransport`** — la couche HTTP : délais, reprises, en-têtes,
désérialisation, avancement. `XtreamHttpTransport` en est l'implémentation
fournie. Elle est séparée du client pour que la politique réseau vive à un seul
endroit, et pour que les tests tournent sans serveur.

**`XtreamClient`** — la surface d'API : une méthode par action Xtream. Il
détient les identifiants, qui n'apparaissent donc dans aucune signature. Toutes
les méthodes lisent ; aucune n'écrit sur le panel.

**`XtreamStreamUrlBuilder`** — assemble les adresses de lecture. Elles ne sont
*pas* renvoyées par l'API : elles sont construites à partir des identifiants et
de l'élément.

---

## 4. Première connexion

```csharp
using XtreamApi;

var credentials = XtreamCredentials.Create(
    "http://panel.example.com:8080", "utilisateur", "motdepasse");

using var client = new XtreamClient(credentials);

// Leve quand les identifiants sont refuses, ou quand le compte est expire,
// desactive ou banni.
var account = await client.AuthenticateAsync(cancellationToken);

Console.WriteLine($"Statut      : {account.User?.Status}");
Console.WriteLine($"Expire le   : {account.User?.ExpiresAt:d}");
Console.WriteLine($"Connexions  : {account.User?.ActiveConnections}/{account.User?.MaxConnections}");
Console.WriteLine($"Formats     : {string.Join(", ", account.User?.AllowedOutputFormats ?? [])}");
```

Si vous ne disposez que d'une adresse de playlist, les identifiants peuvent en
être extraits :

```csharp
var credentials = XtreamCredentials.FromPlaylistUrl(url);

// Variantes sans exception, utiles pendant que l'utilisateur saisit encore :
if (XtreamCredentials.TryFromPlaylistUrl(url, out var parsed)) { /* ... */ }
bool semblePlausible = XtreamCredentials.LooksLikeXtreamPlaylist(url);
```

L'analyse passe par `Uri` et un décodage explicite de la chaîne de requête,
plutôt que par une expression régulière : les mots de passe contenant `%`, `&`
ou `+` sont fréquents et mettent en échec toute approche par motif. Seuls le
schéma, l'hôte et le port sont conservés — ce qui est porté dans le chemin est
écarté, jamais rejoué dans les appels suivants.

---

## 5. Référence des fonctionnalités

### 5.1 État du compte et du serveur

| Méthode | Comportement |
| --- | --- |
| `GetAccountAsync` | Rapporte la réponse du panel telle quelle. Ne lève **pas** en cas de refus : la réponse porte alors `auth = 0`, ce qu'un écran de connexion voudra peut-être afficher. |
| `AuthenticateAsync` | Valide la connexion. Lève `XtreamAuthenticationException` en cas de refus, d'expiration, de suspension ou de bannissement. |

Vérifier `auth == 1` ne suffit pas, et le piège mérite d'être connu : **les
panels répondent couramment `auth: 1` sur un abonnement déjà terminé.**
`UserInfo.IsUsable` combine authentification, statut et expiration, et c'est ce
que teste `AuthenticateAsync`.

Membres utiles sur `UserInfo` : `Status` (`Active`, `Expired`, `Disabled`,
`Banned`, `Unknown`), `IsAuthenticated`, `IsExpired`, `IsUsable`, `IsTrial`,
`ExpiresAt`, `CreatedAt`, `ActiveConnections`, `MaxConnections`,
`AllowedOutputFormats`.

Sur `ServerInfo` : `BaseAddress` (l'hôte de diffusion, assemblé à partir du
protocole, de l'URL et du port que le panel déclare), `TimeZone`, `TimeNow`,
`TimestampNow`, `HttpsPort`, `RtmpPort`.

> `ActiveConnections` face à `MaxConnections` mérite d'être affiché dans toute
> interface. Chaque flux simultané consomme un créneau du quota de
> l'abonnement ; le dépasser fait échouer la requête suivante pour des raisons
> que l'utilisateur trouvera autrement inexplicables.

### 5.2 Catégories

```csharp
var direct = await client.GetLiveCategoriesAsync(cancellationToken);
var films  = await client.GetVodCategoriesAsync(cancellationToken);
var series = await client.GetSeriesCategoriesAsync(cancellationToken);
```

Chaque `XtreamCategory` porte un `Id`, un `Name` et un `ParentId`.

### 5.3 Catalogues — en mémoire ou en flux

Chaque catalogue existe sous deux formes :

```csharp
// En memoire : la liste entiere, une fois qu'elle est toute arrivee.
IReadOnlyList<LiveStream>    chaines = await client.GetLiveStreamsAsync(categoryId, ct);
IReadOnlyList<VodStream>     films   = await client.GetVodStreamsAsync(categoryId, ct);
IReadOnlyList<SeriesSummary> series  = await client.GetSeriesAsync(categoryId, ct);

// En flux : chaque element des qu'il est analyse, sans jamais tenir la
// reponse entiere en memoire.
await foreach (var chaine in client.StreamLiveStreamsAsync(cancellationToken: ct))
{
    // remplit une liste au fur et a mesure de l'arrivee
}
```

**Préférez la forme en flux pour les catalogues non filtrés.** Une seule
réponse `get_live_streams` d'un gros panel dépasse couramment plusieurs
dizaines de mégaoctets ; l'appel en mémoire les retient toutes, plus les objets
analysés, en même temps. Passer un `categoryId` restreint la réponse à la
source, ce qui est toujours moins coûteux que de filtrer après coup.

Membres communs, partagés via le type de base `CatalogItem` : `Num`, `Name`,
`CategoryId`, `CategoryIds`, `AddedAt`, `DirectSource`, `Kind`, `Id`.

`LiveStream` ajoute `StreamId`, `LogoUrl`, `EpgChannelId`, `IsAdult`,
`TvArchive`, `TvArchiveDuration` et le calculé `HasCatchup`.

`VodStream` ajoute `StreamId`, `Title`, `Year`, `PosterUrl`, `Rating`,
`Rating5Based`, `ContainerExtension`, `TmdbId`, `Plot`.

`SeriesSummary` ajoute `SeriesId`, `Title`, `CoverUrl`, `Plot`, `Cast`,
`Director`, `Genre`, `BackdropPaths`, `EpisodeRunTime` ainsi que les deux
orthographes de date de sortie que les panels emploient.

### 5.4 Fiches détaillées

```csharp
VodInfo?    film  = await client.GetVodInfoAsync(streamId, ct);
SeriesInfo? serie = await client.GetSeriesInfoAsync(seriesId, ct);
```

Les deux renvoient `null` quand le panel ne connaît pas l'identifiant, plutôt
que de lever : un identifiant inconnu est une issue ordinaire quand un
catalogue a été rafraîchi sous vos pieds.

`VodInfo` se scinde en `Details` (intrigue, distribution, réalisateur, pays,
genre, durée, note, images, bande-annonce) et `MovieData` (`StreamId`, `Title`,
`Year`, `ContainerExtension`). Les blocs bruts `Video` et `Audio` sont exposés
en `JsonElement` : leur forme dépend du transcodeur que fait tourner le
fournisseur, et leur inventer un modèle commun relèverait de la devinette.

`SeriesInfo` porte `Details`, la liste des `Seasons`, et `Episodes` sous forme
d'un `IReadOnlyDictionary<int, IReadOnlyList<Episode>>` indexé par numéro de
saison. Ce dictionnaire est l'endroit où l'incohérence de l'API se voit le
mieux : un panel le renvoie en tableau tant que les saisons se suivent, puis
bascule en objet dès qu'une saison est retirée. Le convertisseur accepte les
deux.

Chaque `Episode` porte `EpisodeId` — **l'identifiant à lire est celui de
l'épisode, jamais celui de la série** — ainsi que `EpisodeNumber`, `Season`,
`Title`, `ContainerExtension` et un bloc `Details` avec l'intrigue, la durée,
le débit et les visuels.

### 5.5 Guide des programmes

```csharp
// Les prochains programmes d'une chaine.
var suivants = await client.GetShortEpgAsync(streamId, limit: 10, ct);

// La grille complete d'une chaine.
var grille = await client.GetEpgAsync(streamId, ct);

// Le guide entier, XMLTV, en flux.
await using var xmltv = await client.OpenXmltvAsync(ct);
```

`GetEpgAsync` exige un identifiant de chaîne. Certains clients appellent
`get_simple_data_table` sans en fournir et en concluent que le panel ne le
gère pas ; en réalité il ne répond jamais rien d'utile ainsi.

`EpgListing` décode les titres et descriptions en Base64 qu'envoient les
panels, et expose à la fois les heures locales (`StartLocal`, `EndLocal`) et
celles fondées sur l'époque (`StartTimestamp`, `StopTimestamp`), ainsi que
`Duration`, `NowPlaying` et `HasArchive`.

`OpenXmltvAsync` renvoie un flux dont l'appelant dispose. Lisez-le en flux : le
guide complet d'un gros panel peut dépasser la centaine de mégaoctets, et le
mettre en tampon dans une chaîne est un moyen fiable d'épuiser la mémoire.

### 5.6 Fenêtres de rattrapage

```csharp
var rediffusables = await client.GetCatchupTableAsync(
    streamId,
    startInServerTime: heureServeur.AddDays(-2),
    endInServerTime:   heureServeur,
    cancellationToken: ct);
```

Le support de cette action, et la lecture de ses bornes, varient d'un panel à
l'autre — une liste vide ne prouve pas que le rattrapage est indisponible.
`LiveStream.HasCatchup` (à partir de `tv_archive` et `tv_archive_duration`)
reste le signal le plus fiable.

**Les bornes s'expriment dans le fuseau du serveur, pas en UTC.** C'est la
cause la plus fréquente de programmes qui se rejouent avec une ou deux heures
de décalage. `ServerInfo.TimeZone` indique le fuseau dans lequel convertir.

### 5.7 Playlist M3U

```csharp
await using var playlist = await client.OpenPlaylistAsync(
    type: "m3u_plus", output: "ts", cancellationToken: ct);
```

Renvoyée en flux, pour la même raison que XMLTV. Utile quand vous confiez la
liste à un lecteur qui attend une playlist plutôt que de piloter vous-même le
catalogue.

---

## 6. Construction des adresses de lecture

Les adresses de lecture ne font partie d'aucune réponse de l'API. Elles sont
assemblées :

```csharp
// A preferer : le panel peut diffuser depuis un hote different de celui qui
// sert l'API, auquel cas les adresses baties sur l'hote de l'API sont rejetees.
var urls = client.CreateStreamUrlBuilder(account.Server);

Uri chaine  = urls.BuildLive(liveStream);                 // ou (streamId, "ts")
Uri film    = urls.BuildMovie(vodStream);                 // ou (streamId, "mkv")
Uri episode = urls.BuildEpisode(episode);
Uri quelconque = urls.BuildFor(catalogItem);              // aiguille sur Kind

Uri rediffusion = urls.BuildCatchup(
    streamId,
    startInServerTime: debut,       // fuseau du serveur, comme ci-dessus
    duration: TimeSpan.FromMinutes(90));
```

Trois détails que le constructeur gère et qu'une concaténation à la main
ignore :

- **`direct_source` l'emporte quand le fournisseur le fixe.** Certains panels
  imposent une adresse précise par élément ; les surcharges qui prennent un
  objet du catalogue l'honorent automatiquement.
- **Les identifiants sont encodés pour l'URL** dans chaque segment. Un mot de
  passe contenant `/`, `?` ou `#` enverrait sinon l'adresse ailleurs.
- **L'extension de conteneur est normalisée.** Les panels laissent un point
  initial dans `container_extension` assez souvent pour que cela vaille d'être
  traité une fois pour toutes.

Le format demandé doit figurer dans les `AllowedOutputFormats` du compte —
typiquement `ts` pour un flux continu et `m3u8` pour du HLS.

---

## 7. Configuration

```csharp
var options = new XtreamClientOptions
{
    UserAgent               = "VLC/3.0.20 LibVLC/3.0.20",
    ConnectTimeout          = TimeSpan.FromSeconds(15),
    ResponseHeadersTimeout  = TimeSpan.FromSeconds(30),
    RetryCount              = 2,
    RetryBaseDelay          = TimeSpan.FromMilliseconds(500),
    MaxConnectionsPerServer = 8,
    ErrorSnippetLength      = 512,
    AllowInvalidCertificates = false,
};

using var client = new XtreamClient(credentials, options);
```

| Option | Défaut | Pourquoi la changer |
| --- | --- | --- |
| `UserAgent` | `XtreamApi/1.0` | **La première chose à essayer quand un compte valide est refusé.** Beaucoup de panels répondent `401` à une requête dont l'agent leur est inconnu, ou n'acceptent qu'une courte liste d'agents connus. |
| `ConnectTimeout` | 15 s | Établissement TCP et TLS uniquement. |
| `ResponseHeadersTimeout` | 30 s | Borne l'attente des *en-têtes*, jamais celle du corps — voir plus bas. |
| `RetryCount` | 2 | Reprend après un échec passager : coupure, expiration, `5xx`, `429`. Jamais après un refus d'identifiants. |
| `RetryBaseDelay` | 500 ms | Doublé à chaque tentative suivante. |
| `MaxConnectionsPerServer` | 8 | Les appels d'API ne consomment normalement pas le quota de flux de l'abonnement, mais certains panels comptent tout. À baisser si un panel se met à refuser des appels. |
| `ErrorSnippetLength` | 512 | Quelle part d'une mauvaise réponse est conservée pour le diagnostic. |
| `AllowInvalidCertificates` | `false` | Uniquement à la demande explicite de l'utilisateur. Cela désactive la vérification de l'identité du serveur et expose la connexion à une interception. Beaucoup de panels emploient effectivement des certificats auto-signés ou expirés, ce qui est précisément pourquoi cela doit rester un choix délibéré. |

**Les délais s'appliquent en deux temps, à dessein.** `HttpClient.Timeout`
borne la requête entière, corps compris : une valeur globale unique
interromprait donc en plein milieu le transfert parfaitement légitime d'un gros
catalogue. Le transport n'expire donc que sur les *en-têtes* ; la lecture du
corps est bornée par le `CancellationToken` de l'appelant, c'est-à-dire là où
cette décision revient.

---

## 8. Suivi de l'avancement

Le transport rapporte quelle part d'une réponse est arrivée — utile à afficher
sur les gros catalogues, où l'utilisateur fixe sinon une fenêtre figée.

```csharp
using var transport = new XtreamHttpTransport(options);

transport.Progress += (_, p) => Console.WriteLine(
    p.Percentage is { } pct ? $"{pct:0.00} %" : $"{p.BytesReceived:N0} octets");

using var client = new XtreamClient(credentials, transport, ownsTransport: true);
```

`XtreamProgress` fournit `BytesReceived`, `TotalBytes`, `Fraction`,
`Percentage` et `IsMeasurable`.

Deux choses à savoir avant de le brancher sur une barre de progression :

- **`Percentage` vaut `null` quand le serveur n'annonce aucune taille** — soit
  parce qu'il répond par fragments, soit parce qu'il a compressé la réponse,
  auquel cas .NET écarte l'en-tête de longueur pendant la décompression (la
  valeur annoncée couvre les octets compressés et ne correspondrait pas à ce
  qui est lu). Repliez-vous sur `BytesReceived`, toujours exact, ou affichez
  une barre indéterminée.
- **L'événement est levé sur le fil qui lit la réponse, pas sur celui de
  l'appelant.** Un abonné qui touche à l'interface doit revenir vers elle
  lui-même — en WinForms, `Control.BeginInvoke`. L'événement ne distingue pas
  non plus les appels : deux requêtes menées de front entrelacent leur
  avancement.

---

## 9. Gestion des erreurs

Quatre échecs distincts, parce que « quelque chose s'est mal passé » ne dit pas
à un utilisateur s'il doit corriger son mot de passe ou vérifier sa connexion :

```csharp
try
{
    var account = await client.AuthenticateAsync(ct);
}
catch (XtreamAuthenticationException ex)
{
    // Identifiants refuses, ou compte expire / desactive / banni.
    // ex.StatusCode est null quand le refus vient du corps de la reponse.
}
catch (XtreamConnectionException ex)
{
    // Injoignable : DNS, TLS, reseau. ex.IsTimeout separe une expiration d'un
    // refus franc. ex.RequestUri est masquee.
}
catch (XtreamHttpException ex)
{
    // Statut d'erreur. ex.IsTransient vaut true pour 429 et 5xx : a retenter.
}
catch (XtreamProtocolException ex)
{
    // La reponse est arrivee mais est inexploitable. ex.ResponseSnippet en
    // conserve les premiers octets.
}
```

Les quatre dérivent de `XtreamException`, si bien qu'un `catch` unique reste
possible là où la distinction n'importe pas. `XtreamUrlFormatException`
complète l'ensemble pour une adresse malformée.

`ResponseSnippet` justifie sa place : **les panels répondent très souvent
`200 OK` avec une page d'erreur HTML dans le corps.** Sans les premiers octets
de ce qui est réellement arrivé, l'incident est indiagnosticable depuis un
rapport de bogue.

---

## 10. Schémas d'intégration

### 10.1 Usage ponctuel

```csharp
using var client = new XtreamClient(credentials);
```

Le client crée et possède son transport. Pratique pour un script ou une sonde.

### 10.2 Application de longue durée

Créez **un seul transport pour toute l'application** et confiez-le à chaque
client. Chaque transport bâtit son propre `HttpClient`, et en créer un par
appel épuise les sockets même avec un `Dispose` correct : les connexions
s'attardent en `TIME_WAIT`.

```csharp
// Une fois, au demarrage.
var transport = new XtreamHttpTransport(options);

// Par compte, aussi souvent que necessaire. ownsTransport reste false : le
// transport partage survit a chaque client bati dessus.
using var client = new XtreamClient(credentials, transport, ownsTransport: false);
```

### 10.3 Injection de dépendances

```csharp
services.AddSingleton(new XtreamClientOptions { UserAgent = "..." });
services.AddSingleton<IXtreamTransport>(sp =>
    new XtreamHttpTransport(sp.GetRequiredService<XtreamClientOptions>()));

// Les identifiants dependent de la session utilisateur : le client se
// construit la ou ils sont connus, plutot que de s'enregistrer globalement.
services.AddScoped<IXtreamClient>(sp => new XtreamClient(
    CredentialsForCurrentUser(sp),
    sp.GetRequiredService<IXtreamTransport>(),
    ownsTransport: false));
```

Avec `IHttpClientFactory`, utilisez la surcharge du transport qui accepte un
`HttpClient` existant — mais assurez-vous que ce client conserve
`Timeout = Timeout.InfiniteTimeSpan`, sans quoi le délai en deux temps décrit
au §7 est neutralisé et les gros catalogues seront coupés.

### 10.4 Application de bureau

Trois habitudes qui épargnent des ennuis :

- Construire le générateur d'adresses à partir de `account.Server`, et non des
  seuls identifiants, juste après la connexion — puis le garder pour la
  session.
- Remplir les listes avec les méthodes `Stream*Async`, pour que la fenêtre se
  peuple à mesure que la réponse arrive au lieu de rester figée jusqu'à ce
  qu'elle soit entière.
- Revenir vers le fil de l'interface depuis `Progress` (§8).

Le stockage des identifiants relève de l'application, et la bibliothèque ne
prend position que sur un point : ne jamais conserver un mot de passe en clair.
Sous Windows, `ProtectedData` (DPAPI) lie la valeur stockée au compte
utilisateur en quelques lignes.

---

## 11. Durée de vie, threads et annulation

**Libération.** `XtreamClient` ne libère son transport que s'il le possède —
constructeur à un seul argument, ou `ownsTransport: true`. Un transport partagé
doit survivre à tous les clients bâtis dessus et être libéré à l'extinction.

**Threads.** `XtreamClient` ne détient aucun état mutable et s'utilise sans
danger depuis plusieurs tâches à la fois, dans la limite de connexions du
transport. `Progress` est levé sur le fil de lecture.

**Annulation.** Toutes les méthodes prennent un `CancellationToken`, et c'est
la seule borne sur la lecture du corps d'une réponse. Passez-en un vrai : une
fenêtre qui se ferme pendant qu'un gros catalogue arrive doit l'annuler, pas
l'attendre.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var chaines = await client.GetLiveStreamsAsync(cancellationToken: cts.Token);
```

---

## 12. Tolérance aux formats

Les panels Xtream sont follement incohérents, et un client qui les lit
strictement échoue sur la première bizarrerie — emportant la réponse entière
pour un champ que l'appelant n'avait jamais demandé. La tolérance est la raison
d'être de cette bibliothèque, et elle est couverte par 171 tests écrits à
partir de charges utiles que de vrais panels renvoient.

| Ce qui arrive | Traitement |
| --- | --- |
| Un nombre là où une chaîne est déclarée (`tmdb_id`, identifiants) | Lu comme du texte, en préservant les chiffres exacts — aucun arrondi flottant. |
| Une chaîne là où un nombre est déclaré (`"1080"`, `"7.5"`) | Analysée en culture invariante. |
| `""`, `"0000-00-00"`, `"null"` pour une date | Donne `null` au lieu de lever. |
| Époques Unix, `yyyy-MM-dd HH:mm:ss`, ISO 8601 | Toutes acceptées pour un même champ. |
| Titres et descriptions EPG en Base64 | Décodés de façon transparente. |
| Un tableau qui devient un objet dès qu'une entrée disparaît | Les deux acceptés (saisons, listes de catégories). |
| Une valeur seule là où une liste est déclarée | Enveloppée dans une liste d'un élément. |
| `null` à l'intérieur d'une collection | Ignoré au lieu d'interrompre l'analyse. |

`XtreamJson.Default` expose les `JsonSerializerOptions` configurés si vous avez
besoin d'analyser vous-même une charge utile brute avec la même tolérance.

---

## 13. Étendre la bibliothèque

**Remplacer le transport.** `IXtreamTransport` compte quatre méthodes —
`GetJsonAsync`, `StreamJsonArrayAsync`, `GetTextAsync`, `OpenReadAsync` — plus
l'événement `Progress`. Implémentez-le pour tester sans serveur, pour ajouter
un cache devant le panel, ou pour journaliser chaque appel :

```csharp
public sealed class CachingTransport(IXtreamTransport inner) : IXtreamTransport
{
    public event EventHandler<XtreamProgress>? Progress
    {
        add    => inner.Progress += value;
        remove => inner.Progress -= value;
    }

    public Task<T?> GetJsonAsync<T>(
        XtreamCredentials credentials, XtreamRequest request, CancellationToken ct)
        => /* consulter le cache, sinon inner.GetJsonAsync<T>(...) */;

    // ...
}
```

Le cache mérite considération pour les catégories, qui changent rarement et
sont demandées à chaque démarrage.

**Appeler une action que le client n'enveloppe pas.**
`XtreamRequest.ForAction` construit n'importe quel appel `player_api.php`, et
le transport le portera :

```csharp
var request = XtreamRequest.ForAction("some_action", ("param", "valeur"));
var result  = await transport.GetJsonAsync<JsonElement>(credentials, request, ct);
```

**Substituer le client dans les tests.** Toutes les méthodes reposent sur
`IXtreamClient` : un modèle de vue se teste contre un bouchon, sans le moindre
HTTP.

---

## 14. Dépannage

| Symptôme | Cause probable |
| --- | --- |
| `401` sur des identifiants qui fonctionnent ailleurs | Le `User-Agent`. Choisissez-en un que le panel accepte — c'est de loin la cause la plus fréquente. |
| `AuthenticateAsync` lève, `GetAccountAsync` réussit | Conforme à la conception : le compte s'authentifie mais il est expiré, suspendu ou banni. Lisez `UserInfo.Status` et `ExpiresAt`. |
| `XtreamProtocolException` sur un appel qui fonctionnait | Lisez `ResponseSnippet`. C'est en général une page d'erreur ou de maintenance HTML servie avec un `200 OK`. |
| Les adresses de lecture sont refusées alors que l'API répond bien | Le panel diffuse depuis un autre hôte. Construisez les adresses depuis `account.Server`, pas depuis les identifiants. |
| Une rediffusion est décalée d'une ou deux heures | Les bornes ont été passées en UTC. Elles doivent être dans le fuseau du serveur — `ServerInfo.TimeZone`. |
| Un gros catalogue est coupé en cours de route | Un `HttpClient.Timeout` global a été laissé en place sur un client fourni. Il doit valoir `Timeout.InfiniteTimeSpan`. |
| Les appels échouent après quelques requêtes parallèles | Le panel compte les appels d'API dans le quota de connexions. Baissez `MaxConnectionsPerServer`. |
| Une erreur TLS sur un panel qu'un navigateur ouvre | Certificat auto-signé ou expiré. `AllowInvalidCertificates` existe pour cela, mais faites-en un choix explicite de l'utilisateur : il supprime la garantie de parler au serveur que l'on croit. |
| L'avancement n'affiche jamais de pourcentage | Le serveur n'a annoncé aucune taille — réponse par fragments ou compressée. Utilisez `BytesReceived`, ou une barre indéterminée. |

---

## Voir aussi

- [`usage-guide.md`](usage-guide.md) — la version anglaise de ce guide.
- [`xtream-api-reference.md`](xtream-api-reference.md) — le protocole : chaque
  action, ses paramètres, ses pièges de typage et les formats d'adresse.
- `README.md` — installation, notes de conception, et l'avertissement
  **Intended use and disclaimer** qui gouverne tout ce qui précède.
