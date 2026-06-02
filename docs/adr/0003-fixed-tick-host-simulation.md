# Fixed-tick host simulation

The host will run combat simulation at a fixed 60 Hz tick and remain authoritative for movement, hits, health, cooldowns, deaths, round state, and scoring. This makes networked combat timing and bug reproduction more predictable than variable-delta or client-authoritative simulation.
