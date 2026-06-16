// Contrato para algo que puede reaccionar a ser parreado (un ataque enemigo, el boss...).
// El jugador, al parrear con éxito, llama a OnParried() sobre el origen del ataque para
// que sufra stun, pérdida de postura, etc. El "qué pasa al ser parreado" lo decide quien
// lo implemente, sin que el jugador necesite conocer tipos concretos.
public interface IParryable
{
    void OnParried();
}
