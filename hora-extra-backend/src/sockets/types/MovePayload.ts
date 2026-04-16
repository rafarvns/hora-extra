/**
 * Payload para sincronização de movimento de alta frequência.
 * Nomes de chaves curtos ('p' para posição e 'r' para rotação) 
 * para otimização de banda em pacotes de socket.
 */
export interface IPlayerMovePayload {
    p: [number, number, number]; // [x, y, z]
    r: number;                   // Rotação Y (Yaw)
}

/**
 * Update emitido pelo servidor para todos os clientes em uma sala.
 */
export interface IStateUpdatePayload {
    players: Array<{
        id: string;
        p: [number, number, number];
        r: number;
    }>;
}
