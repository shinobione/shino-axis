# AXIS — Architecture cible (hypothèse, non validée)

**Statut :** proposition de travail. **Aucun langage, framework ou modèle de plugin n'est acté.**

## Problème à résoudre

Des produits SHINO fiables existent déjà, mais leurs entrées/sorties, leurs démons Windows, leurs modes de lancement et leurs clients distants ont grandi séparément. AXIS veut **unifier l'expérience et les services réutilisables**, sans transformer tous les programmes en un monolithe ni tout réécrire.

## Frontières proposées

1. **Shell Windows** : fenêtre principale, navigation, paramètres, tray, installation et diagnostics. Il ne lit pas directement les capteurs.
2. **Runtime d'intégration** : inventaire des modules, gestion de leur cycle de vie, santé, sources de données, état courant, bus d'événements, commandes, permissions et versioning.
3. **Adaptateurs** : utilisent les APIs, exports ou processus existants de CONTROL, LINK/RELAY, PERISCOPE, BRAIN et autres. Ils ne détiennent pas simultanément la même autorité de publication.
4. **Passerelle appareils** : point d'accès réseau unique avec appairage, auth, contrats versionnés, compatibilité et observabilité. Ne pas exposer directement les services loopback.
5. **Clients** : Android TV, firmware SmallTV et futurs appareils. Chaque client rend localement les données qu'il sait prendre en charge.

Un seul produit installé ne veut pas dire un seul processus. Le runtime doit pouvoir superviser des processus isolés quand c'est raisonnable.

## Enveloppes conceptuelles minimales

Les formats ci-dessous sont des **concepts à spécifier**, pas des API livrées :

- **Source** : ID, propriétaire, version de schéma, unité, horodatage de mesure, origine, valeur et qualité.
- **State** : dernier état valide, statut d'ancienneté, TTL, indisponibilité et dérivation contrôlée.
- **Event** : nom, ID, temps d'occurrence, ID de corrélation, version et données limitées.
- **Command** : action explicite, cible, émetteur, autorisation, ID idempotent, accusé de réception et résultat.
- **Capability** : aptitudes déclarées du client, formats pris en charge, contraintes réseau/mémoire/affichage.

Les propriétés critiques (fraîcheur, provenance, absence, statut UNVERIFIED) ne peuvent pas être effacées par une transformation de données.

## Contrats existants : préserver avant d'étendre

L'application Android TV possède un contrat snapshot HUB v2, avec TLS, authentification lecture seule, référence de pochette, domaines PC/média/Codex et TTL spécifiques. La roadmap locale validée au 01/11/2026 signale Android 2.1.2, RELAY 0.5.7 et HUB 0.5.1 ; GitHub main peut être plus ancien.

- Ne pas inventer des quotas, des missions Codex, des latences ou des mesures.
- Ne jamais lancer un second producteur concurrent pendant une migration.
- Respecter les permissions propres aux lecteurs et aux producteurs.
- Construire des fixtures compatibles pour tests de régression, puis tester sur appareils réels après autorisation.

## Windows et communication

- Application locale native, **sans VM obligatoire** pour les fonctions hébergées par Windows.
- API locale interne distincte de la passerelle LAN; aucune ouverture de ports par défaut sans consentement et configuration sécurisée.
- Appairage explicite, jetons limités par rôle, TLS si requis, révocation, limitation des requêtes et journalisation expurgée.
- Reconnexion, cache, veille/reprise, détection d'instance unique et redémarrage non destructif.
- Pas de dépendance à une API IA, à un modèle local chargé en mémoire ou au cloud pour les fonctions ordinaires.

## Contraintes matérielles

- Android TV : application Kotlin existante ; navigation télécommande et affichage réellement vérifié sur Freebox.
- SmallTV : microcontrôleur à mémoire limitée, LCD 240×240, firmware stable à confirmer sur l'appareil. Ne pas imposer de navigateur/WebSocket/JSON volumineux sans test des budgets.
- PC éteint : aucune télémétrie Windows live. Les fonctions autonomes (ex. météo) exigent une source indépendante : conserver le service existant jusqu'à décision explicite.

## Pistes à comparer, pas à entériner

- Hôte **.NET / ASP.NET Core** + shell WPF/WebView2 ou Avalonia : affinité Windows/C# PERISCOPE, mais coût d'une couche d'intégration à créer.
- **Tauri/Rust**, **Wails/Go** : options possibles, surtout si avantage mesuré.
- **Macro Deck 3**, **ioBroker**, **Node-RED** : évaluer les contrats de modules, la faisabilité d'un shell SHINO et le poids de l'architecture.
- Réutilisation de bibliothèques/outils existants possible seulement après vérification de leurs licences et dépendances.

### Critères de décision

Parité des données, intégration réelle de CONTROL et PERISCOPE sans réécriture, sécurité du LAN, facilité d'ajout de modules, robustesse au redémarrage, facilité d'installation, ressources sur le PC cible, reproductibilité, licence et maintenance future.

## Non-objectifs de Foundation

- Réécriture de CONTROL, PERISCOPE, LINK ou des clients existants.
- Flash ou mise à jour OTA, modification du HUB ou de la VM.
- Plugin marketplace, sandbox universelle, scripting libre non sécurisé.
- Assistant IA permanent ou nouvelle infrastructure cloud.
- Réorganisation de tous les dépôts SHINO.

## Risques ouverts

- L'état local Windows peut être différent des branches GitHub.
- Le protocole embarqué SmallTV et le chemin météo doivent être mesurés, pas devinés.
- Les plugins natifs dans un processus commun ne sont pas une frontière de sécurité.
- Les choix de licences doivent précéder toute copie de code tiers.
- Une interface unique ne doit pas masquer l'état réel de ses services.

Chaque changement d'architecture doit être noté avec alternatives, preuves et mécanisme de retour arrière.
