using UnityEngine;
using System.Collections.Generic;

public class AttackHitbox : MonoBehaviour
{
    public int attackDamage = 10;
    public string targetTag = "Enemy";

    // Necesitaremos una referencia al Collider2D de este hitbox
    private Collider2D hitboxCollider;
    private List<Collider2D> collidersAlreadyHitThisSwing;

    void Awake()
    {
        // Obtenemos la referencia al Collider2D de este GameObject (el hitbox)
        hitboxCollider = GetComponent<Collider2D>();
        if (hitboxCollider == null)
        {
            Debug.LogError("AttackHitbox no tiene un Collider2D adjunto!");
        }
    }

    void OnEnable()
    {
        if (collidersAlreadyHitThisSwing == null)
        {
            collidersAlreadyHitThisSwing = new List<Collider2D>();
        }
        else
        {
            collidersAlreadyHitThisSwing.Clear();
        }
        Debug.Log("AttackHitbox.OnEnable - Lista 'collidersAlreadyHitThisSwing' reiniciada. Count: " + collidersAlreadyHitThisSwing.Count + ". Frame: " + Time.frameCount);

        // --- NUEVA LÓGICA: Comprobación de Superposición Manual al Habilitar ---
        CheckForOverlapAndDealDamage();
    }

    // OnTriggerStay2D sigue siendo útil si el enemigo ENTRA al hitbox MIENTRAS ya está activo,
    // o si el hitbox es grande y el enemigo se mueve dentro de él durante el swing.
    void OnTriggerStay2D(Collider2D otherCollider)
    {
        // Debug.Log("AttackHitbox.OnTriggerStay2D - Detectado: " + otherCollider.gameObject.name + " (Tag: " + otherCollider.gameObject.tag + "). Frame: " + Time.frameCount);
        ProcessHit(otherCollider);
    }

    // Método para la comprobación de superposición manual
    private void CheckForOverlapAndDealDamage()
    {
        if (hitboxCollider == null) return;

        // Creamos un filtro para solo detectar colliders en la capa del enemigo (si la tienes configurada)
        // y que no sean el propio hitbox. Por ahora, solo usaremos el tag.
        ContactFilter2D contactFilter = new ContactFilter2D();
        // contactFilter.SetLayerMask(enemyLayerMask); // Podrías añadir una LayerMask para enemigos
        contactFilter.useTriggers = true; // Queremos interactuar con otros colliders, no solo triggers

        List<Collider2D> overlappingColliders = new List<Collider2D>();
        int count = Physics2D.OverlapCollider(hitboxCollider, contactFilter, overlappingColliders);

        // Debug.Log("CheckForOverlap - Colliders encontrados: " + count);

        foreach (Collider2D hitCollider in overlappingColliders)
        {
            // Asegurarnos de no golpearnos a nosotros mismos o a otros hitboxes del jugador
            if (hitCollider.gameObject == gameObject || hitCollider.gameObject.transform.IsChildOf(transform.root))
            {
                continue; // Saltar este collider si es parte del jugador
            }
            ProcessHit(hitCollider);
        }
    }

    // Método unificado para procesar un golpe, llamado desde OnTriggerStay2D o CheckForOverlap
    private void ProcessHit(Collider2D otherCollider)
    {
        if (otherCollider.gameObject.CompareTag(targetTag))
        {
            if (!collidersAlreadyHitThisSwing.Contains(otherCollider))
            {
                Debug.Log("AttackHitbox.ProcessHit - " + otherCollider.gameObject.name + " NO está en la lista de este swing. Aplicando daño...");

                EnemyHealth enemyHealth = otherCollider.gameObject.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(attackDamage);
                    collidersAlreadyHitThisSwing.Add(otherCollider);
                }
                else
                {
                    Debug.LogWarning("El objeto " + otherCollider.gameObject.name + " tiene el tag '" + targetTag + "' pero no tiene el componente EnemyHealth.");
                }
            }
        }
    }

    // OnDisable sigue igual o puedes añadir un log si quieres
    // void OnDisable() { ... }
}