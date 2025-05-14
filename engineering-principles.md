# RTL Engineering Principles

These principles serve as AI guardrails and apply across all levels of software development. Use them in system design, solution architecture, and coding practices. When prompting AI (e.g., Cursor), refer to the **Prompt Cues** to guide compliant code generation.

---

## 1. Secure by Default

**Why it matters:** Security lapses damage trust. We minimize attack surfaces and build resilience from the start.

**System Design**
- Use least privilege for all components.
- Avoid exposing internal services to the internet.
- Define authentication/authorization at the system boundary.

**Solution Design**
- Use established identity providers (e.g., Auth0, Keycloak).
- Apply input validation and output encoding.
- Avoid secrets in code — use secret managers.

**Code Practices**
- No hardcoded credentials.
- Sanitize user input.
- Use security linters and scanning tools.

**Prompt Cue**
> "Ensure that this service follows our 'Secure by Default' principle: no secrets in code, sanitize all input, and follow least privilege."

---

## 2. Observable and Measurable

**Why it matters:** Without visibility, issues go unnoticed. Observability enables faster recovery and continuous improvement.

**System Design**
- Emit structured logs, metrics, and traces for every critical component.
- Integrate health checks and error tracking (e.g., Sentry, Prometheus).

**Solution Design**
- Define SLIs and SLOs upfront.
- Use correlation IDs across services.

**Code Practices**
- Log key business events with context.
- Use trace decorators on critical functions.

**Prompt Cue**
> "Add structured logging and traces to enable observability per our principle — include correlation ID and error context."

---

## 3. AI-Ready and Evolvable

**Why it matters:** Systems should enable future automation, data extraction, and adaptability.

**System Design**
- Separate core logic from interfaces and external integrations.
- Design APIs and events to be self-descriptive and versioned.

**Solution Design**
- Use clean architecture and vertical slice architecture.
- Avoid tight coupling to vendors or frameworks.

**Code Practices**
- Keep logic small, testable, and expressive.
- Document APIs with OpenAPI and use consistent naming.

**Prompt Cue**
> "Structure this code using vertical slice architecture and keep it AI-ready: clean logic, descriptive interfaces, no tight coupling."

---

## 4. Simplicity over Cleverness

**Why it matters:** Simpler systems are more maintainable and safer to evolve.

**System Design**
- Prefer fewer components with well-defined responsibilities.
- Avoid unnecessary abstraction layers.

**Solution Design**
- Choose boring tech unless complexity is justified.
- Design for clarity, not elegance.

**Code Practices**
- Use simple, readable constructs.
- Avoid overengineering (no premature optimization or meta-programming).

**Prompt Cue**
> "Refactor this to favor simplicity and readability — avoid unnecessary abstraction or clever patterns."

---

## 5. Decoupled and Domain-Aligned

**Why it matters:** Aligning with business domains reduces complexity and makes systems easier to reason about.

**System Design**
- Design systems around bounded contexts.
- Minimize cross-domain dependencies.

**Solution Design**
- Apply DDD: aggregates, events, value objects.
- Expose APIs only for the owning domain’s capabilities.

**Code Practices**
- Keep domain logic pure and isolated.
- Avoid leaking internal models across boundaries.

**Prompt Cue**
> "Make sure this logic aligns with the domain model and doesn't leak internal abstractions across services."