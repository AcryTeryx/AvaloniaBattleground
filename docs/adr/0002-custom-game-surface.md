# Custom game surface for combat rendering

Gameplay will render through an Avalonia custom-drawn game surface while Avalonia controls handle shell screens, lobby UI, and HUD. This avoids modeling every fighter, projectile, and attack effect as layout-driven UI controls, which would make the combat loop harder to keep predictable.
