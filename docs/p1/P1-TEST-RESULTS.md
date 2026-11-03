# P1 — Résultats de tests

**2 novembre 2026 : A 14/14 ; B SDK 14/14.** Aucun échec, erreur ou test ignoré dans les rapports finaux [A](../../lab/p1/shared/results-a.json) / [B](../../lab/p1/shared/results-b.json). Bootstrap et compilations finales A/B : code retour 0, zéro erreur, zéro avertissement. Ces résultats qualifient le laboratoire aux frontières décrites, pas deux produits complets ni des appareils physiques.

## Identité du banc

Base AXIS `5a32a5ce1d50f52e5b438d5fa9b9f162ea14a9de`, branche `codex/p1-foundation-lab`, travail P1 non committé à la livraison. P0 inchangé. SDK 10.0.403 local épinglé ; Macro Deck v3.0.0 `614dc0bbb04be21434176208fadff45179a6d5ec`, sources tierces sans modification suivie. Python 3.12 disponible. Commandes complètes dans [README du laboratoire](../../lab/p1/README.md).

Corpus d'exemples SHA-256 : `70e3c08f3e6ef93518288e7b1b3af93b7c5ea8fc2d7718c155cb51bad27c9c5d`. Horloge contractuelle injectée depuis le 1er janvier 2030 UTC, pas l'horloge du PC. Même test source et même corpus source hashés dans les deux rapports JSON. Le hash d'exemples n'est pas à lui seul le hash de tous les scénarios : leurs mutations sont fixées par le hash de `test_candidates.py`. IDs de sessions/événements, ports et répertoires de runs sont volontairement uniques ; aucune comparaison ne repose sur leur égalité.

Les schémas existants sont chargés à l'identique en lecture seule (empreintes dans les rapports) ; ils ne sont ni modifiés, ni recopiés dans AXIS. LINK_SIM produit les métriques HUB et un supplément numérique SmallTV fictif. PERISCOPE_SIM produit quotas et resets fictifs, sans Codex réel. Les clients sont des assertions HTTP Python, pas les applications natives.

## Matrice exécutée

Chaque ligne correspond à une méthode de test black-box commune, lancée dans un processus A puis dans un processus B distinct. Les assertions multiples sont incluses dans chaque scénario ; « 14 » n'est pas un compte de toutes les assertions.

| Scénario | Vérifications | A | B SDK |
|---|---|---|---|
| Deux sources/deux clients | CPU/quota/provenance TEST, quota absent null, activité UNVERIFIED, projection numérique distincte, domaine Codex refusé côté SmallTV, faux bearer refusé | PASS | PASS |
| TTL exacts | 6/10/900 s : TTL−1 ms CURRENT, TTL et TTL+1 ms STALE ; capturedAt inchangé | PASS | PASS |
| Capteur absent | available=false/value=null conservé, aucune transformation en zéro ; projection SmallTV refusée | PASS | PASS |
| Reset quota/UNKNOWN | reset exact rend périmé, absence source reste UNKNOWN | PASS | PASS |
| Rejeu/temps | égal identique idempotent ; conflit même date, recul, capture future et fraîcheur contradictoire refusés, dernier bon état conservé | PASS | PASS |
| Matrice réseau schémas | ancien document accepté par les deux ; extension connue refusée par RELAY/acceptée par Android ; version inconnue et type invalide refusés | PASS | PASS |
| Sémantique réseau/cache | date réseau future refusée ; network devient STALE à la borne de 6 s dans le snapshot reconstruit | PASS | PASS |
| Rôles/parse/volume | lecteur et mauvais propriétaire refusés ; clés dupliquées, NaN, CPU hors borne et corps excessif refusés | PASS | PASS |
| Événement/commande/arrêt | événement version 1 ; commande autorisée, ACK ACCEPTED/PENDING distinct du résultat ; répétition idempotente, conflit de cible refusé ; arrêt LINK_SIM garde Codex CURRENT et telemetry expire | PASS | PASS |
| Reprise durable | watermark conservé après arrêt/redémarrage, replay ancien refusé ; commande PENDING devient UNKNOWN_NO_RETRY, exécution ultérieure refusée | PASS | PASS |
| SmallTV réception | corps 16..384, JSON HUB et formes invalides refusés, CPU/température bornés ; réception 6000 ms encore valide et 6001 ms périmée | PASS | PASS |
| Complément SmallTV | date des données VRAM/puissance différente de la capture projetée : refus | PASS | PASS |
| Extension | ClockModule enregistré, producteurs précédents conservés ; B : le snapshot décodé de la réponse `variables/get` SDK est égal à celui lu par HTTP et garde capturedAt | PASS | PASS |
| Images/timeline/version | référence artwork 40961 octets refusée/40960 acceptée ; position>durée et schemaVersion 1 refusés | PASS | PASS |

Le test d'image porte sur la **référence et ses bornes**, pas sur le décodage JPEG, son transport ni sa taille effective. Le test de capacité porte sur le corps HTTP, pas sur une saturation exhaustive du magasin de commandes. Le verrou d'écrivain existe dans l'implémentation mais sa contention inter-processus n'est pas testée. Le replay durable est testé après arrêt propre ; panne électrique, disque plein/corruption et crash pendant renommage restent NON TESTÉS.

## Limites qui empêchent la qualification complète

- Les 14 tests B utilisent le même `LabEngine` que A. Ils ne démontrent pas que variables, événements ou commandes natifs Macro Deck portent nativement toutes les garanties HUB. Seule la lecture de variable est vérifiée par une invocation protocolaire SDK réelle. Les commandes/événements du banc passent par `/lab`, pas par les actions/événements natifs Macro Deck.
- `MacroDeckTestHost` et le plugin B sont hébergés dans un processus de laboratoire ; le produit complet, son superviseur de processus, son interface Tauri/Angular et ses intégrations natives ne sont pas exécutés. Aucun crédit de performance ou d'isolation de processus ne leur est attribué.
- La fenêtre A compile et permet l'observation mais n'est pas rendue/inspectée dans cette mission. Tray, installation, autostart, veille/reprise et expérience Windows complète restent NON TESTÉS/non livrés lorsqu'indiqué.
- Aucun transport de production TLS 1.3/CA/SAN/pin DER ni Digest SmallTV testé. Les bearers sont fictifs et locaux ; pas de test réel contre un appareil, de pairing ni d'autorité installée. Les routes `/hub/v1/*` ne sont pas exposées par ce laboratoire.
- Les producteurs sont des fonctions indépendantes du corpus, pas des services autonomes avec supervision. L'arrêt vérifié est logique (refus d'une nouvelle publication), pas le crash isolé d'un collecteur hors processus.

## Échecs conservés et corrections

Les dossiers `.runtime/test-*/<run-unique>/` restent ignorés et non supprimés. Ils contiennent logs et états synthétiques, dont les runs échoués. Les traces brutes ne sont pas versionnées pour éviter chemins privés et données d'authentification du seul banc SDK. Résumé expurgé des échecs réellement rencontrés :

1. Réseau Git initial : DNS bloqué dans le sandbox ; accès officiel réussi avec les permissions réseau de session. Pas de faux résultat « tag absent ».
2. Premier test : FAIL attendu car CandidateA.dll n'existait pas encore.
3. Build A : namespace incorrect pour ApplicationConfiguration ; remplacé par EnableVisualStyles ; compilation vérifiée ensuite.
4. HTTP loopback initial : WinError 10013 dans le sandbox. Tests relancés avec permission réseau limitée à la tâche ; aucun changement du pare-feu.
5. Validation initiale : JsonSchema conservait un JsonElement appartenant à un JsonDocument libéré. Le log identifiait ObjectDisposedException ; clone du schéma avant libération, puis 10/10 PASS.
6. SDK B : identifiant `test_snapshot` invalide selon le SDK (IDs avec tirets) ; corrigé en `test-snapshot`. Un build lancé avant la fin du premier run B a temporairement rencontré des DLL verrouillées et huit avertissements de copie. Les vérifications finales sont séquentielles et sans avertissement.
7. Test de reprise ajouté : FAIL PENDING au lieu de UNKNOWN_NO_RETRY ; conversion conservatrice à la reprise et refus d'exécution, puis PASS.
8. Tests de réseau périmé et de complément SmallTV désynchronisé : FAIL constatés avant correction ; recalcul de fraîcheur réseau et contrôle de date/provenance, puis PASS.
9. Inventaire de licences : un nuspec d'un autre namespace XML faisait échouer la lecture ; sélecteur XML indépendant du namespace, puis inventaire généré. Aucun « MIT » supposé pour un package sans vérification de ses métadonnées.

Incident distinct : .NET a signalé la génération automatique d'un certificat de développement lors du premier lancement du SDK. Ce comportement est désactivé dans le bootstrap livré, sans commande de confiance ni nettoyage du magasin réel.

## Revue et statut

Revue ciblée par l'auteur des frontières du banc et des preuves ; une consultation Advisor unique, indépendante et en lecture seule, a examiné le confinement du démarrage B et la portée des résultats. Ce n'est pas une revue indépendante exhaustive du code. Aucun verdict global PASS de P1, aucun benchmark long, aucune opération de production/publication/appareil. Prochaine action et critère dans [la comparaison](P1-COMPARISON.md).
