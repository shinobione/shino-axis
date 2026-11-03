# AXIS P1 — laboratoire entièrement TEST

> Archive de comparaison P1, indépendante du produit AXIS V0.8. Son état « pas de fondation validée » décrit ce laboratoire à sa date ; le produit a ensuite adopté .NET 10/WPF. Pour construire AXIS, suivre le [guide actuel](../../docs/DEVELOPMENT.md), sans exécuter ce bootstrap.

**État : résultats fonctionnels partiels ; pas de fondation validée.** A est un hôte ASP.NET Core avec fenêtre WinForms optionnelle. B est un plugin exercé avec le **banc de protocole officiel du SDK Macro Deck**, pas l'application Macro Deck complète. Le moteur contractuel commun appartient au laboratoire ; ses succès ne prouvent pas les garanties natives de Macro Deck.

## Reproduire

Windows x64, Python 3.12 et Git. Depuis ce dossier :

```powershell
./bootstrap.ps1
$relaySchema = Join-Path $env:USERPROFILE 'Documents/SHINO-LAB/SHINO-RELAY/contracts/snapshot-v2.schema.json'
$androidSchema = Join-Path $env:USERPROFILE 'Documents/SHINO Television Android/contracts/snapshot-v2.schema.json'
python shared/test_candidates.py --candidate a --relay-schema $relaySchema --android-schema $androidSchema --report shared/results-a.json
python shared/test_candidates.py --candidate b --relay-schema $relaySchema --android-schema $androidSchema --report shared/results-b.json
python shared/inventory_dependencies.py
```

Le bootstrap vérifie SDK 10.0.403 et son SHA-512, clone le tag B dans le cache, vérifie le SHA, restaure en mode verrouillé depuis NuGet officiel et compile. Aucune installation globale. Il désactive la génération automatique du certificat ASP.NET. Les schémas existants sont seulement lus ; aucune copie de leur contenu n'est redistribuée. Vérifier leurs hashes dans les rapports JSON pour reproduire le même état local que P1. Sur une autre machine, fournir des fichiers de schéma autorisés de mêmes empreintes via les arguments ; ne pas utiliser une copie distante plus ancienne en prétendant reproduire le test.

Les accès HTTP peuvent nécessiter la permission réseau de la session même en loopback. Ne pas désactiver les règles du pare-feu. Le banc lance ses propres enfants cachés, ports IPv4 éphémères ; chaque cas possède un dossier unique `.runtime/`, ses logs et son état. Les anciens états ne sont pas recyclés. Arrêt normal par une commande TEST ; recours à la terminaison du seul enfant possédé si l'arrêt échoue. Aucun balayage ni arrêt global de processus.

## Frontières réellement testées

`LINK_SIM` et `PERISCOPE_SIM` sont deux fonctions productrices indépendantes du corpus Python, sans collecte réelle ni planificateur autonome. Le processus Python joue aussi ANDROID_SIM (lectures/sémantique) et SMALLTV_SIM (réception numérique). Ces rôles ne sont pas des applications natives Android/ESP8266. B ajoute une vraie session HTTP/WebSocket SDK et une invocation `variables/get` ; ses APIs de host sont celles du banc synthétique.

Transport du laboratoire : `POST /lab`, bearer fictif public `TEST_ADMIN`, `TEST_LINK`, `TEST_PERISCOPE` ou `TEST_READER`. **Ces identités ne sont pas des credentials utilisables en production.** Le laboratoire n'implémente pas le transport TLS/Digest de production, les routes `/hub/v1/*` complètes ni un remplacement de HUB. L'état snapshot garde le format v2 et les schémas sont évalués par JsonSchema.Net ; les règles additionnelles exercées figurent dans les tests.

Le supplément SmallTV (`gpu_vram_mb`, `gpu_power`) vient du corpus LINK_SIM, avec marque TEST et date identique à la mesure projetée. Il ne vient pas du schéma HUB qui ne porte pas ces champs. Une donnée partielle/expirée ne se transforme pas en un zéro sain pour l'appareil.

## Interface A

La fenêtre est compilée mais son rendu n'est pas qualifié. Exemple de lancement manuel, après bootstrap, dans un nouveau répertoire TEST :

```powershell
$runDir = Join-Path $PWD ('.runtime/gui-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDir | Out-Null
& .cache/dotnet/dotnet.exe candidate-a/bin/Release/net10.0-windows/CandidateA.dll $runDir $relaySchema $androidSchema --gui
```

La fenêtre montre modules et snapshot, avec l'horloge du laboratoire. Elle ne crée pas de données ; injecter le corpus par le protocole de test. Aucun tray/autostart/service Windows n'est installé. Fermer la fenêtre arrête l'hôte. Ne pas utiliser cette fenêtre comme preuve de fonctionnement du shell final AXIS.

## Arrêt / retour arrière

Les tests attendent la fin de chaque enfant avant le cas suivant. Le produit Macro Deck complet n'est jamais lancé par ces commandes. Les dépendances, sources tierces et sorties restent dans `.cache/`, `.runtime/`, `bin/`, `obj/` ignorés. Les conserver pour analyser les échecs ; aucune suppression récursive automatique. Aucun changement des produits SHINO, appareil, VM, registre, service, pare-feu, push ou merge n'est requis.

Incident d'environnement à conserver : lors du tout premier appel du SDK local dans cette session, avant le bootstrap durci, .NET a signalé la création automatique d'un certificat de développement ASP.NET. Aucune commande de confiance ni suppression de certificat n'a été exécutée ; le magasin réel n'a pas été inspecté. Ce signalement n'est pas masqué par la désactivation ajoutée ensuite.
