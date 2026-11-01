# P0 — Recommandation et laboratoire P1

**Décision proposée [H] : fédérer par adaptateurs, préserver les autorités actuelles et comparer deux hôtes isolés. Aucun langage ni socle retenu, aucune migration autorisée.** Les sources montrent des contrats réutilisables mais des identités d'installation inconnues et des schémas locaux divergents ; voir [inventaire](P0-INVENTORY.md), [contrats](P0-CONTRACTS.md), [risques](P0-RISKS.md).

## Matrice de réutilisation

| Composant | Orientation proposée | Justification et coût relatif |
|---|---|---|
| CONTROL Core + collector | **Garder**, adapter une projection en lecture seule | G : moteur Node autonome, API loopback et isolation Chrome. Réécrire reprise/déduplication serait coûteux ; projection minimale à définir, sans état privé brut. |
| PERISCOPE | **Garder**, adapter un export contractuel futur | L/D : quotas fiables séparés d'activité indicative. C# WinForms .NET Framework n'implique pas DLL compatible avec tout hôte .NET moderne ; frontière de processus préférable. Coût faible à moyen pour export, élevé pour fusion UI/métier. |
| LINK / lecteurs réutilisés par RELAY | **Garder**, adapter les sorties existantes | L : capteurs et expéditeur éprouvés dans leurs sources, sans export public du worker actif démontré. Réutilisation possible après revue de licence et frontière ; ne pas ajouter une acquisition concurrente. |
| RELAY | **Garder** comme publicateur intermédiaire | L : transport, normalisation et autorité durables déjà présents. Remplacement coûteux/risqué tant que parité des états inconnus et du binding n'est pas prouvée. |
| HUB / validateurs LAB | **Garder**, réutiliser concepts et vecteurs | L/D : schémas, règles temporelles, ACL et watermarks. Portabilité possible par tests croisés ; porter seulement JSON Schema perdrait des invariants. Aucun retrait de VM proposé. |
| Android TV | **Garder** natif | L/G : client Kotlin existant, version locale avancée. Coût d'une nouvelle UI sans bénéfice démontré ; effort ciblé sur contrats et parité. |
| SmallTV | **Garder** firmware et protocole existants | L : budgets embarqués et Digest distincts ; N : identité installée. Un adaptateur borné coûte moins qu'un nouveau client volumineux, sous réserve de qualification ultérieure. |
| Passerelle Android v1 | **Adapter** seulement si encore nécessaire | L : chemin historique parallèle. Migration/retrait envisageable après inventaire des consommateurs réels et parité v2 ; aucun arrêt maintenant. |
| BRAIN / SHINO-OS | **Différer**, optionnels | N : code actuel non inspecté. Ni dépendance Foundation ni obstacle aux fonctions ordinaires. |
| Collecteurs, tray, autostart, stockage répétés | **Migrer éventuellement**, domaine par domaine | L/G : mécanismes séparés recensés. Une UI commune peut superviser plusieurs processus ; consolidation après mesures, sans fusion obligatoire. |
| Composants à retirer maintenant | **Aucun** | N : absence de preuve qu'un composant installé est inutile. |

Les coûts sont des estimations relatives H, pas des durées chiffrées. Les bibliothèques existantes offrent des points de réutilisation (psutil, GSMTC/PyWinRT, Pillow, jsonschema, APIs Node, rendu Kotlin) ; versions, licences et notices doivent être fixées avant toute copie. S/firmware GPL ne constitue pas une bibliothèque permissive à incorporer silencieusement.

## Architecture minimale candidate

```text
Shell Windows (UI/tray/diagnostics)
             │ lecture d'état / actions explicitement permises
Hôte de modules connus : registre + cache borné + santé + routage
             │
Adaptateurs hors processus vers sorties SHINO existantes
             │
Contrats existants conservés → HUB / clients conservés
```

Proposition H fondée sur A/ARCHITECTURE et les frontières L/G relevées :

1. Deux adaptateurs en lecture seule ; registre statique de modules, pas de découverte de plugins arbitraires.
2. État minimal : domaine/source, version de contrat, valeur, unité, capturedAt, provenance, disponibilité, qualité/fraîcheur et TTL. Préserver les enveloppes existantes à la frontière ; ne pas imposer une nouvelle enveloppe aux appareils.
3. Événement borné avec ID, instant et corrélation ; commande à cible/droit explicites, ID idempotent et résultat distinct de l'ACK. Dans P1, commande purement synthétique sans effet OS.
4. Capacités déclarées des clients simulés ; validation des limites avant projection. Rejet explicite d'une version inconnue, pas de suppression silencieuse de champs.
5. Supervision limitée aux enfants que le laboratoire possède. Les applications existantes restent autonomes. Stockage isolé, journaux expurgés, arrêt/reprise sans effacement d'incertitude.
6. En P2 éventuel, pas de passerelle LAN nouvelle par défaut : conserver HUB ; tout remplacement réseau appartient à une mission ultérieure autorisée. CONTROL reste loopback.

Alternatives H : une réécriture monolithique supprimerait certaines frontières mais multiplie les régressions ; un bus/plugin universel simplifie l'extension théorique mais ajoute droits et exploitation avant la preuve d'utilité. Les sources ne justifient ni l'une ni l'autre à P0.

## P1 — Protocole reproductible proposé, non exécuté

### Deux expériences comparables

- **A, hôte Windows minimal** : candidat .NET/ASP.NET Core avec interface minimale, ou autre pile justifiée avant démarrage. Affinité Windows et coexistence avec C# à tester, pas avantage mesuré. A/ARCHITECTURE le cite ; aucune version SDK n'est sélectionnée ici [H/D].
- **B, plateforme existante** : Node-RED comme candidat documentaire initial. N1 manifeste consulté : version déclarée 5.0.9, Node ≥22.9, licence racine Apache-2.0, nombreuses dépendances dont Express/AJV. Ces faits G ne prouvent ni une release installable qualifiée, ni un shell Windows/tray satisfaisant. Fixer commit/release, intégrité et licences transitives avant essai. Macro Deck/ioBroker restent alternatives non auditées, pas des solutions rejetées.

Choisir Node-RED pour **l'expérience comparative** est une proposition H : modèle de flux à confronter aux contrats SHINO, coût de dépendances et de shell à mesurer. Le MVP doit pouvoir montrer qu'une plateforme existante ne convient pas. Aucune recommandation définitive de produit/langage.

### Gel du banc avant tout essai

1. Après autorisation P1 distincte, répertoire jetable par candidat, aucune référence aux coffres/données/apps existants. Relever OS, CPU/RAM, alimentation, versions runtimes, commit et état source, dépendances/licences, paramètres et hash du corpus. Aucun paquet installé dans P0.
2. Corpus **entièrement synthétique TEST** : source 1 = télémétrie style LINK ; source 2 = quotas style PERISCOPE. Même chronologie et graine pour A/B, horloge injectée. Ne lancer ni LINK, ni RELAY, ni PERISCOPE, ni Codex RPC réels.
3. Deux clients simulés : lecteur Android/HUB v2 avec règles de fraîcheur ; consommateur SmallTV borné qui reçoit une projection de télémétrie. La consommation SmallTV du quota n'est pas prétendue existante : domaine non supporté doit être refusé. Tests HTTP éventuels exclusivement loopback avec identités TEST et ports éphémères, après autorisation P1.
4. Harness et corpus communs, sans exigences de code partagées avec les produits. Exécutions A/B séquentielles ; aucun serveur simultané. Toutes les commandes d'essai et le nettoyage doivent être documentés avant mesure.

### Scénarios et assertions communs

| Cas | Résultat obligatoire proposé [H, fondé sur P0-CONTRACTS] |
|---|---|
| Deux sources, deux clients | Parité des valeurs/unités/dates ; aucune source réelle appelée ; quotas non supportés rejetés côté SmallTV simulé. |
| TTL | Tester t=TTL−1 ms, TTL, TTL+1 ms, date future et reset quota ; CURRENT/STALE/UNKNOWN conformes. Tester séparément le seuil SmallTV >6000 ms. |
| Absence | GPU absent ≠ 0 ; quota partiel/null ; aucune mission ni fin inventée ; cache déconnecté clairement marqué. |
| Contrats | Ancien v2 sans réseau, variante réseau connue/inconnue, v1 projeté avec pertes ; champs supplémentaires/non-finis/doublons rejetés selon frontière. |
| Événement + commande | Un événement versionné reçu ; commande synthétique autorisée appliquée une fois ; répétition idempotente ; rôle incorrect refusé sans effet. |
| Reconnexion / rejeu | Coupure simulée, même observation idempotente, conflit à date égale et recul temporel refusés ; ne pas re-dater. |
| Arrêt/crash module | Autre module reste lisible ; absence se périme ; redémarrage conserve watermark et ambiguïté synthétiques ; pas d'ACK fabriqué. |
| Nouveau module | Ajouter un troisième adaptateur synthétique par configuration/point d'extension, sans modifier les domaines des deux premiers ; consigner fichiers et temps. |
| Image / bornes | Référence/hash/borne 40960 octets pour HUB ; payload numérique SmallTV borné ; pas d'image injectée dans metrics. |
| Sécurité | Refus lecteur→publication, taille excessive, certificat/pin TEST incorrect si TLS exercé ; aucune écoute hors loopback. |

### Mesures et gate

Proposition H : même machine et conditions, 3 répétitions par candidat, 60 s de chauffe puis 10 min au repos et 10 min de charge synthétique identique. Alterner l'ordre A/B. Consigner CPU normalisé, working set et mémoire privée de **toute l'arborescence de processus**, médiane/p95/max ; latence acquisition synthétique→client, délai reprise, taille installée, dépendances, temps d'intégration/débogage et fichiers touchés pour le troisième module. Ni CPU cumulé isolé ni instantané unique ne valent baseline.

Barrières éliminatoires : tous les cas contractuels critiques passent, zéro accès aux sources privées, zéro double producteur, zéro sortie LAN/appareil, arrêt/nettoyage propres et licence documentée. Un module qui masque STALE/UNKNOWN est disqualifié même s'il consomme moins.

Avant essais, convenir des budgets absolus CPU/RAM adaptés au PC cible ; ils ne sont pas mesurés ni fixés arbitrairement ici. Comparer ensuite complexité, ressources, maintenabilité, installation/reprise et coût de l'extension. Produire commandes exactes, corpus hashé, CSV synthétique, journal expurgé et ADR avec alternatives/repli ; aucune moyenne pondérée ne compense un échec éliminatoire.

Retour arrière P1 : arrêter seulement les enfants du banc, vérifier la fermeture des listeners TEST, conserver les résultats synthétiques dans AXIS ; enlever uniquement l'environnement jetable prévu. Aucun remplacement d'exécutable installé, service, registre, HUB ou firmware. Une décision de socle après P1 ne vaut pas autorisation de migration [H ; A/AGENTS].

## Résultat P0 et prochaine action unique

P0 livre un inventaire sourcé, les flux, contraintes, risques et protocole comparatif ; aucune preuve d'installation ou performance nouvelle. Les cinq rapports sont documentaires et ne développent pas AXIS. Pas de tests métier/build exécutés ; vérification limitée aux livrables et aux sources énumérées.

**Prochaine action prioritaire : faire valider le mandat P1 isolé décrit ici, avec les deux candidats exacts et les budgets CPU/RAM avant tout développement ou installation.** Critère de réussite : corpus/scénarios identiques, dépendances épinglées et droits limités au banc sont approuvés ; les inconnues de production restent explicitement hors de l'essai. Cette action transforme les hypothèses de socle en comparaison mesurable sans nécessiter de migration ni d'accès aux appareils.
