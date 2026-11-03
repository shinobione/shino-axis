# CODEX Live — plan et décisions

Mandat du 03/11/2026 : livraison complète locale WPF, sans PROJECTS, Chrome,
PERISCOPE, appareils ni publication. Exécution par le modèle principal.

Conserver le lecteur RPC éphémère unique et le lecteur JSONL PERISCOPE déjà
porté. Une nouvelle infrastructure de collecte n'apporterait pas de preuve
supplémentaire. Les fins sont celles des tours locaux, jamais des conversations.

- [x] Tests puis acquisition : 30/60/120/300 s, sans chevauchement, délai RPC
  20 s existant, backoff exponentiel borné à 300 s, démarrage et reprise immédiats.
  Historique : valeurs identiques dédupliquées avec point de continuité à 5 min ;
  les erreurs marquent une rupture, sans accumulation d'erreurs identiques.
- [x] Tests puis observation : départs espacés de 5 s, expiration rapport 25 s,
  preuve activité 90 s, dernière activité visible, reprise invalidant les preuves
  antérieures et supprimant les notifications de rattrapage.
- [x] WPF : cartes conservées, chronomètre par seconde, anneau WPF seulement
  pour activité récente vérifiée, terminal retenu 8 s avant liste récente bornée.
- [x] Tests natifs, vérification réelle des horloges WPF et captures synthétiques,
  build Debug et publication locale Windows x64 autonome V0.8, empreintes et rapport.

Points de revue : horloge revenant en arrière ; réponse en vol à la reprise ;
désactivation pendant lecture ; données identiques après interruption ; sous-agents
et missions simultanées. Pas de sommeil physique ni notification réelle imposés
à la machine. Aucune écriture dans les profils de production pendant la recette.

Résultats : tests de cadence/historique initialement rouges, puis corrigés ; deux
régressions cache/redémarrage et recul horloge également reproduites avant correction.
Revue indépendante ciblée en lecture seule : aucun constat matériel. Garde de
reprise ajoutée aux projections/notifications ; annulation du RPC en vol avant
relance, sans concurrence. Exécution dans le checkout courant demandée par le
mandat, sans manipulation Git ni déplacement des nombreux fichiers non suivis.
