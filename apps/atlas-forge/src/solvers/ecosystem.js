import { habitat, N_PER_BIOMASS } from "../core/ecosystem.js";
import { neighbors, area, coord, distance } from "../geometry/sphere.js";
/** Annual aggregate resource model. Stocks kg/m²; no individual animals or implicit energy. */
export function ecologyYear(world, env) {
  const eco = world.state.ecosystem,
    catalog = world.state.species,
    n = eco.nutrients.length;
  if (!env.transport) {
    env.areas = Array.from({ length: n }, (_, i) =>
      area(
        Math.floor(i / eco.grid.width),
        eco.grid,
        world.definition.planet.radius,
      ),
    );
    env.suitable = Array.from(
      { length: n },
      (_, i) =>
        new Map(
          catalog.map((s) => [
            s.id,
            habitat(s, env.elevation[i], env.temperature[i], env.rain[i]),
          ]),
        ),
    );
    env.transport = {};
    for (const s of catalog)
      env.transport[s.id] = Array.from({ length: n }, (_, i) => {
        const targets = neighbors(i, eco.grid).filter(
          (j) => env.suitable[j].get(s.id) > 0,
        );
        return targets.map((j) => [
          j,
          Math.min(
            0.2,
            (s.dispersalKm * 1000) /
              Math.max(
                1,
                distance(
                  coord(i, eco.grid),
                  coord(j, eco.grid),
                  world.definition.planet.radius,
                ),
              ),
          ) / targets.length,
          env.areas[i] / env.areas[j],
        ]);
      });
  }
  const ledger = { productionKg: 0, respirationKg: 0 };
  for (let i = 0; i < n; i++) {
    const a = env.areas[i];
    // Decay recycles nitrogen; organic carbon leaves this stock as respiration.
    const decayed = eco.detritus[i] * (1 - Math.exp(-0.2));
    eco.detritus[i] -= decayed;
    eco.nutrients[i] += decayed * N_PER_BIOMASS;
    ledger.respirationKg += decayed * a;
    const suitable = env.suitable[i];
    // Competition for the same mineral pool is proportional, independent of catalog order.
    const growth = catalog
      .filter((s) => s.role === "producer")
      .map((s) => ({
        s,
        want:
          (s.productivity * suitable.get(s.id) * eco.biomass[s.id][i]) /
          (0.1 + eco.biomass[s.id][i]) /
          (1 + eco.biomass[s.id][i] / 3),
      }));
    const requested = growth.reduce((sum, g) => sum + g.want, 0),
      factor = requested
        ? Math.min(1, eco.nutrients[i] / (N_PER_BIOMASS * requested))
        : 0;
    for (const { s, want } of growth) {
      const amount = want * factor;
      eco.biomass[s.id][i] += amount;
      eco.nutrients[i] -= amount * N_PER_BIOMASS;
      ledger.productionKg += amount * a;
    }
    eco.nutrients[i] = Math.max(0, eco.nutrients[i]);
    // All predators/herbivores request prey simultaneously, then share the available stock.
    const requests = new Map();
    for (const s of catalog.filter((s) => s.role !== "producer")) {
      const available = s.diet.reduce(
        (sum, id) =>
          sum + (id === "detritus" ? eco.detritus[i] : eco.biomass[id][i]),
        0,
      );
      if (!available) continue;
      const demand = eco.biomass[s.id][i] * s.consumption * suitable.get(s.id);
      if (!demand) continue;
      for (const prey of s.diet) {
        const stock =
          prey === "detritus" ? eco.detritus[i] : eco.biomass[prey][i];
        const list = requests.get(prey) || [];
        list.push({ s, want: (demand * stock) / available });
        requests.set(prey, list);
      }
    }
    const gains = new Map();
    let waste = 0;
    for (const [prey, list] of requests) {
      const stock =
          prey === "detritus" ? eco.detritus[i] : eco.biomass[prey][i],
        demand = list.reduce((sum, r) => sum + r.want, 0),
        amount = Math.min(stock, demand);
      if (!demand) continue;
      if (prey === "detritus") eco.detritus[i] -= amount;
      else eco.biomass[prey][i] -= amount;
      for (const { s, want } of list) {
        const meal = (amount * want) / demand,
          assimilated = meal * s.efficiency;
        gains.set(s.id, (gains.get(s.id) || 0) + assimilated);
        waste += meal - assimilated;
      }
    }
    eco.detritus[i] += waste;
    for (const s of catalog) {
      eco.biomass[s.id][i] += gains.get(s.id) || 0;
      const mortality = s.mortality + (1 - suitable.get(s.id)) * 0.6;
      const dead = eco.biomass[s.id][i] * (1 - Math.exp(-mortality));
      eco.biomass[s.id][i] -= dead;
      eco.detritus[i] += dead;
    }
  }
  // Conservative area-weighted dispersal; habitat and land/water barriers apply.
  for (const s of catalog) {
    if (!s.dispersalKm || s.managed) continue;
    const stock = eco.biomass[s.id],
      delta = new Float64Array(n);
    for (let i = 0; i < n; i++) {
      if (!stock[i]) continue;
      for (const [j, f, ratio] of env.transport[s.id][i]) {
        const amount = stock[i] * f;
        delta[i] -= amount;
        delta[j] += amount * ratio;
      }
    }
    for (let i = 0; i < n; i++) stock[i] = Math.max(0, stock[i] + delta[i]);
  }
  eco.year++;
  return ledger;
}
