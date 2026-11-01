# SHINO // AXIS — Roadmap de fondation

**Statut :** discovery / design. **Pas de développement en production autorisé par cette roadmap.**

## P0 — Audit des vrais systèmes en fonctionnement

**Entrées** : dépôts GitHub, worktrees locaux, documentation, exécutables installés, contrats et services. Ne pas confondre ces sources.

À relever : versions et SHA, ports/processus, autostart, producteurs, état de LINK/RELAY/HUB, schémas snapshot, permissions, stockage, secrets (présence uniquement), baselines CPU/RAM, séquences veille/redémarrage, fonctionnement Android TV et SmallTV.

**Sortie** : matrice `conserver / adapter / migrer / retirer`, carte des flux, risques et correspondance `GitHub ↔ local ↔ appareil`.

**Gate** : pas de migration envisagée tant que les responsabilités et les sources autoritaires ne sont pas connues.

## P1 — Choix du socle par preuves

Construire deux expériences jetables en environnement isolé :

- une base Windows minimale utilisant des briques .NET (ou autre candidat justifié) ;
- une plateforme open source existante (Macro Deck, ou meilleur candidat après audit).

**Même scénario** : deux producteurs de natures différentes, un état versionné, un événement, une commande autorisée, deux clients simulés, une reconnexion, un arrêt d'un module et un nouveau module ajouté sans patcher le domaine d'un autre.

Mesurer : temps d'intégration, facilité de debug, CPU/RAM au repos, dépendances, licence, installation Windows, tests et reprise. Ne retenir aucun résultat sans protocole reproductible.

**Gate** : ADR explicite choisissant le socle, avec alternatives rejetées et preuves.

## P2 — Foundation minimale

Livrer un hôte Windows unifié (interface, tray, configuration, diagnostics), les premiers contrats, un système de modules **simple et testable**, et des adaptateurs en lecture seule pour au moins deux sources existantes. Pas de duplication de données ni de modification de leurs dépôts.

**Gate** : ajout/suppression d'un module sans casser les autres ; mécanismes de retour arrière et journalisation lisible.

## P3 — Communication appareils

Adapter la passerelle Windows aux contrats déjà consommés par Android TV et SmallTV, en commençant par des tests sur fixtures/simulateurs. Vérifier les rôles d'authentification, la fraîcheur des données et les reconnexions.

**Gate** : parité réelle du flux Android TV et conservation du firmware SmallTV stable, après validation physique autorisée.

## P4 — Bascule progressive

Un domaine à la fois : mesurer la parité, basculer l'autorité de publication, conserver un rollback, puis seulement désactiver les processus redondants. Ne pas supprimer automatiquement HUB/RELAY/VM.

**Gate** : absence de double publication et d'impact sur les autres domaines.

## P5 — Preuve d'extensibilité

Créer un module **entièrement nouveau** (exemple : profil Windows lié à l'application au premier plan) sans modification de la logique métier du noyau.

**Gate** : modèle de module documenté, tests, installation et désinstallation propres.

## Principes de livraison

- Commits et PR de petite portée, sans diffusion de données privées.
- Tests synthétiques ≠ preuve sur matériel réel.
- Aucun changement dangereux, publication de code privé ou activation réseau sans accord explicite.
- Documenter le **dernier état vérifié**, les écarts, et **une prochaine action prioritaire**.
- Préférer les briques existantes et le quota Codex minimal ; éviter les réaudits et refontes graphiques sans preuve.

**Première action :** réaliser P0 sous forme d'audit lecture seule.
