import { execSync } from 'child_process';

const PORT = 3000;

try {
    console.log(`[CLEAN] Procurando processos na porta ${PORT}...`);
    // Comando para Windows que encontra o PID na porta e o mata
    const findPidCmd = `netstat -ano | findstr :${PORT} | findstr LISTENING`;
    const output = execSync(findPidCmd).toString();
    
    const lines = output.trim().split('\n');
    lines.forEach(line => {
        const parts = line.trim().split(/\s+/);
        const pid = parts[parts.length - 1];
        if (pid && pid !== '0') {
            console.log(`[CLEAN] Matando processo PID ${pid} ocupando a porta ${PORT}...`);
            execSync(`taskkill /F /PID ${pid} /T`);
        }
    });
} catch (e) {
    // Se não encontrar nada, o netstat retorna erro (exit 1 em findstr), ignoramos.
}

try {
    // Mata qualquer outro processo node residual, exceto o atual
    // (Note: isso pode matar o npm que está rodando este script, mas como é pre-dev, o nodemon iniciará depois)
    // Para ser mais seguro no Windows, apenas matamos node que NÃO seja o atual se possível, 
    // mas taskkill /IM node.exe é efetivo e o predev é isolado.
    console.log(`[CLEAN] Limpando processos node.exe residuais...`);
    // execSync(`taskkill /F /IM node.exe /T`); 
    // ^ Se rodarmos isso agora, matamos o próprio script. 
    // Então só matamos na porta, o que é o principal problema.
} catch (e) {}

console.log(`[CLEAN] Ambiente limpo!`);
