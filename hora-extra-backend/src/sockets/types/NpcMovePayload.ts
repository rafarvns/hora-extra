/**
 * Payload para sincronização de NPCs e Bosses.
 */
export interface INpcMovePayload {
    id: string;                  // Identificador Único da Entidade (ex: "guard_01")
    p: [number, number, number]; // Posição [x, y, z]
    r: number;                   // Rotação Y (Yaw)
}

/**
 * Payload para registro inicial de um NPC na sala.
 */
export interface INpcRegisterPayload {
    id: string;
    type: 'npc' | 'boss';
    p: [number, number, number];
    r: number;
    name?: string;
    isMaster?: boolean;
}
