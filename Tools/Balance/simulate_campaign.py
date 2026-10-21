"""Current five-stage offline statistical model; never starts Unity or changes game tuning.

Source constants/weights are read on each run. Decision policies and human delay are
assumptions, not a prediction of human win rates or a Unity Play verification.
"""
from __future__ import annotations

import argparse
from collections import Counter
import copy
import csv
from datetime import datetime, timezone
import hashlib
from functools import lru_cache
import json
import math
from pathlib import Path
import random
import re
import statistics as st
import sys
import time
from concurrent.futures import ProcessPoolExecutor, as_completed

ROOT = Path(__file__).resolve().parents[2]
DEST = ROOT / 'output/verification/latest/campaign-balance'
SOURCE = ROOT / 'Assets/_COLA_COLA/Scripts'
ICE,CANDY,QUEUE,BILL,SPICY,SAFETY,PIZZA,HOT,STAMP,OPENER,STRAW,SODA,COIN,SWAP,SALT,COOL,PUNCH,LID,EXCHANGE = range(19)
DISCOUNT,LIGHT,BOLD,BREAKAGE,SERVICE,TIDY,SALTY,COOLING,HOTDEAL,RECYCLE,STORAGE,DANGER = range(12)
SUPPLY,ROUGH,CRISIS,PREPAID,ACCIDENT = range(12,17)


def load_rules():
    paths = ['Core/ColaItemBalance.cs', 'Core/BottlePressureModel.cs', 'Core/ColaItemCatalog.cs',
             'Core/ColaProgressionCatalog.cs', 'Presentation/ColaItemController.cs',
             'Presentation/ColaRunProgression.cs', 'Presentation/ColaBossItemAI.cs',
             'Presentation/ColaDuelController.cs', 'Presentation/ColaShakeAnimator.cs',
             'Presentation/ColaBurstPresentation.cs', 'Presentation/ColaItemPresentation.cs',
             'Presentation/ColaWaiterBellPress.cs', 'Presentation/ShakeSelectionInteraction.cs',
             'Core/ColaBossTactics.cs']
    sources = {p: (SOURCE / p).read_text(encoding='utf-8-sig') for p in paths}
    def const(src, name):
        return float(re.search(r'\b' + name + r'\s*=\s*([\d.]+)', sources[src]).group(1))
    settings_path = ROOT / 'Assets/_COLA_COLA/Data/PressureInteractionSettings.asset'
    settings = settings_path.read_text(encoding='utf-8-sig')
    def setting(name): return float(re.search(name + r':\s*([\d.]+)', settings).group(1))
    scene = (ROOT / 'Assets/Scenes/MainScene.unity').read_text(encoding='utf-8-sig')
    catalog = sources['Core/ColaItemCatalog.cs']
    entries = re.findall(r'new\(ColaItemId\.(\w+), "([^"]+)"', catalog)
    weight_source = catalog.split('StageWeights =', 1)[1].split('public static int DropWeightFor', 1)[0]
    weights = [list(map(int, row.split(','))) for row in re.findall(r'new\[\] \{([\d,]+)\}', weight_source)]
    ability_source = sources['Core/ColaProgressionCatalog.cs'].split('Achievements =', 1)[0]
    abilities = re.findall(r'\(ColaAbilityId\.(\w+), "([^"]+)", \$?"([^"]+)"', ability_source)
    hp_match = re.search(r'BossHealthForStage\(int level\) => level <= 1 \? (\d+) : level == 2 \? (\d+) : (\d+)', sources['Presentation/ColaDuelController.cs'])
    hp1, hp2, hp3 = map(int, hp_match.groups())
    values = {name: const('Core/ColaItemBalance.cs', name) for name in
              ('IceReduction','CandyIncrease','SafetyProbabilityReduction','SpicyProbabilityIncrease',
               'OpenerReduction','StrawTokens','CoinCupTokens','HotDamageBonus','StampRewardBonus',
               'SaltIncrease','CoolingReduction','PunchTokens','LidIncrease',
               'MediumAbilityReward','HighAbilityReward','LowHealthThreshold','LowHealthProbabilityReduction','SavingsTokenThreshold','SavingsRewardBonus','SupplyItemCount','RoughPressureBonus',
               'CrisisProbabilityReduction','PrepaidDiscount','AccidentTokenBonus','TicketTokens','ReturnTokens')}
    values.update(steps=[int(const('Core/BottlePressureModel.cs', n)) for n in ('LowPressure','MediumPressure','HighPressure')],
        minimum=int(setting('minimumBottleLimit')), maximum=int(setting('maximumBottleLimit')),
        exponent=setting('probabilityExponent'), player_hp=int(re.search(r'playerMaxHealth: (\d+)', scene).group(1)),
        boss_hp=[hp1, hp2, hp3, hp3, hp3], weights=weights, items=entries, abilities=abilities,
        boss_start_items=[int(x) for x in re.search(r'StartingItems = \{([^}]+)\}',sources['Core/ColaBossTactics.cs']).group(1).split(',')],
        boss_start_tokens=[int(x) for x in re.search(r'StartingTokens = \{([^}]+)\}',sources['Core/ColaBossTactics.cs']).group(1).split(',')],
        cost=int(const('Presentation/ColaItemController.cs','WaiterCost')),
        pack=int(const('Presentation/ColaItemController.cs','WaiterItemCount')),
        capacity=int(const('Presentation/ColaItemController.cs','Capacity')),
        boss_think=float(re.search(r'bossThinkSeconds: ([\d.]+)', scene).group(1)),
        shake_duration=const('Presentation/ColaShakeAnimator.cs','Duration'),
        result_check=const('Presentation/ColaShakeAnimator.cs','ResultCheckTime'),
        grip_end=const('Presentation/ColaShakeAnimator.cs','GripEndTime'))
    values.update(revision=int(const('Core/ColaItemBalance.cs','Revision')),
        safe_rewards=[int(const('Core/ColaItemBalance.cs', n)) for n in ('LowSafeReward','MediumSafeReward','HighSafeReward')],
        danger_threshold=const('Core/ColaItemBalance.cs','DangerPayThreshold'),
        danger_bonus=int(const('Core/ColaItemBalance.cs','DangerPayBonus')),
        targeted_pressure=True, ability_rework=True, safety_stacking=True, service_next_order=True, service_capacity_safe=True, coupon_on_acquisition=True, tactics={n:const('Core/ColaBossTactics.cs',n) for n in
            ('AttackStage','DefenseStage','MinimumComboGain','MinimumAttackImprovement','DefenseRiskThreshold',
             'MinimumDefenseReduction','StageFourTemperature','StageFiveTemperature','StageFourExploration','StageFiveExploration')})
    assert len(entries) == 19 and len(abilities) == 17 and len(weights) == 5
    assert all(len(row) == 19 and sum(row) == 100 for row in weights)
    values['item_rework'] = True
    values['shared_abilities'] = True
    values['boss_ability_counts'] = [int(x) for x in re.search(r'StartingAbilities = \{([^}]+)\}',sources['Core/ColaBossTactics.cs']).group(1).split(',')]
    priority = sources['Core/ColaBossTactics.cs'].split('AbilityPriority =',1)[1].split('};',1)[0]
    ids = {row[0]: i for i,row in enumerate(abilities)}
    values['boss_ability_priority'] = [ids[name] for name in re.findall(r'ColaAbilityId\.(\w+)',priority)]
    values['source_sha256'] = {str((SOURCE/p).relative_to(ROOT)): hashlib.sha256((SOURCE/p).read_bytes()).hexdigest() for p in paths}
    values['source_sha256'][str(settings_path.relative_to(ROOT))] = hashlib.sha256(settings_path.read_bytes()).hexdigest()
    values['source_sha256']['Assets/Scenes/MainScene.unity'] = hashlib.sha256((ROOT/'Assets/Scenes/MainScene.unity').read_bytes()).hexdigest()
    return values


def life(hp): return 3 if hp <= 1 else 1.7 if hp == 2 else 1.25 if hp == 3 else 1
def coin(tokens, cost): return .10 if tokens < cost else .055 if tokens < cost * 2 else .02


@lru_cache(maxsize=65536)
def pressure_prediction(step, pressure, limit, exponent, bonus, reduction, increase):
    after = pressure + max(0, step + bonus)
    risk = 1. if after >= limit else max(0., min(1., (after / limit)**exponent - reduction + increase))
    return after, risk


class Campaign:
    def __init__(self, seed, rules, policy='balanced', ability_mode='random', first=None, start_stage=1):
        self.rules = rules
        self.policy, self.ability_mode, self.first = policy, ability_mode, first
        self.drop = random.Random(seed ^ 0x28D7)
        self.roll = random.Random(seed ^ 0x915A)
        self.limits = random.Random(seed ^ 0x1911)
        self.choices = [random.Random(seed ^ 0x43A1), random.Random(seed ^ 0x719B)]
        self.ability_rng = random.Random(seed ^ 0x75B2)
        self.inv = [[], []]
        self.tokens = [0, 0]
        self.owned = set()
        self.boss_owned = set()
        self.boss_service_ready = True
        self.boss_recycling = 0
        self.boss_pending_coupons = 0
        self.pending_supply = [0,0]
        self.first_bottle_order = [True,True]
        self.fx = [[False,0,0,False,0,0], [False,0,0,False,0,0]]  # stamp, safety stacks, spicy stacks, chilled, incoming pressure
        self.stage = start_stage - 1
        self.limit = 0
        self.hot = self.hot_drawn = False
        self.bursts = self.picks = self.recycling = 0
        self.pending = self.finished = self.completed = self.censored = False
        self.records = []
        self.item_generated = Counter()
        self.item_used = Counter()
        self.ability_effects = {i: Counter() for i in range(len(rules['abilities']))}
        self.ability_at_pick = []
        self._utility_cache = {}
        self._reply_cache = {}
        self.service_ready = True
        self.pending_coupons = 0
        self.begin_stage(fresh_start=True)

    def has(self, who, ability): return ability in (self.owned if who==0 else self.boss_owned)
    def capacity(self, who): return self.rules['capacity'] + (2 if self.has(who, STORAGE) else 0)
    def cost(self, who): return max(1,self.rules['cost'] - self.has(who,DISCOUNT) -
        (self.rules['PrepaidDiscount'] if self.has(who,PREPAID) and self.first_bottle_order[who] else 0))
    def pack(self, who):
        ready = (self.service_ready if who==0 else self.boss_service_ready) if self.rules.get('service_next_order') else self.stage_orders[who] == 0
        if (who==0 or self.rules.get('shared_abilities')) and self.rules.get('service_capacity_safe'):
            ready = ready and self.capacity(who)-len(self.inv[who]) > self.rules['pack']
        return self.rules['pack'] + (self.has(who, SERVICE) and ready)

    def clear_evaluation(self):
        self._utility_cache.clear(); self._reply_cache.clear()

    def flush_coupons(self):
        while self.pending_coupons > 0 and len(self.inv[0]) < self.capacity(0):
            self.inv[0].append(EXCHANGE); self.item_generated[EXCHANGE] += 1
            self.ability_effects[TIDY]['items'] += 1
            self.pending_coupons -= 1
        while self.boss_pending_coupons > 0 and len(self.inv[1]) < self.capacity(1):
            self.inv[1].append(EXCHANGE); self.item_generated[EXCHANGE] += 1
            self.boss_pending_coupons -= 1
        for who in (0,1):
            while self.pending_supply[who] > 0 and len(self.inv[who]) < self.capacity(who):
                self.inv[who].extend(self.draw(int(self.rules['SupplyItemCount'])))
                self.pending_supply[who] -= int(self.rules['SupplyItemCount'])
                if who==0:self.ability_effects[SUPPLY]['items']+=int(self.rules['SupplyItemCount'])

    def draw(self, count, exclude=None):
        allow_hot = not self.hot_drawn and not self.hot and all(HOT not in bag for bag in self.inv)
        row = self.rules['weights'][self.stage - 1]
        pool = [i for i, w in enumerate(row) if w and i != exclude and (i != HOT or allow_hot)]
        result = []
        for _ in range(min(count, len(pool))):
            ticket = self.drop.randrange(sum(row[i] for i in pool))
            for i in pool:
                ticket -= row[i]
                if ticket < 0:
                    result.append(i); pool.remove(i); break
        if HOT in result: self.hot_drawn = True
        self.item_generated.update(result)
        return result

    def new_bottle(self):
        r = self.rules
        limit = self.limits.randint(r['minimum'], r['maximum'])
        if limit == self.limit and r['maximum'] > r['minimum']:
            limit = r['minimum'] + (limit - r['minimum'] + 1) % (r['maximum'] - r['minimum'] + 1)
        self.limit, self.pressure, self.hot = limit, 0, False
        self.hot_drawn = any(HOT in bag for bag in self.inv)
        # Pending next-shake effects belong to the actor, so they survive replacement bottles.
        self.first_bottle_order = [True,True]
        for who in (0,1):
            if self.has(who,SUPPLY):self.pending_supply[who] += int(r['SupplyItemCount'])

    def begin_stage(self, fresh_start=False):
        self.stage += 1
        self.hp = [self.rules['player_hp'], self.rules['boss_hp'][self.stage - 1]]
        self.maxhp = self.hp[:]
        self.fx = [[False,0,0,False,0,0], [False,0,0,False,0,0]]
        self.hot = self.hot_drawn = False
        self.stage_orders = [0, 0]
        self.service_ready = self.boss_service_ready = True
        self.boss_pending_coupons = self.boss_recycling = self.pending_supply[1] = 0
        count = self.rules.get('boss_ability_counts',[0]*5)[self.stage-1]
        self.boss_owned = set(self.rules.get('boss_ability_priority',[])[:count])
        if self.has(1,TIDY):self.boss_pending_coupons += 1
        if fresh_start:
            self.inv[0] = self.draw(2, PIZZA)
        self.tokens[1] = self.rules.get('boss_start_tokens',[0]*5)[self.stage-1]
        self.inv[1] = []
        self.inv[1] = self.draw(self.rules.get('boss_start_items',[2]*5)[self.stage-1], PIZZA)
        # ResetSession starts the new stage's outcome/limit stream; RNG distribution is preserved.
        self.limit = 0
        self.new_bottle()
        if TIDY in self.owned:
            if self.rules.get('coupon_on_acquisition'):
                self.pending_coupons += 1
            elif len(self.inv[0]) < self.capacity(0):
                self.inv[0].append(EXCHANGE); self.item_generated[EXCHANGE] += 1
                self.ability_effects[TIDY]['items'] += 1
            else: self.ability_effects[TIDY]['missed_full'] += 1
        self.flush_coupons()
        self.actor = 0
        self.rec = dict(stage=self.stage, won=False, censored=False, auto=0., player_turns=0, boss_turns=0,
            player_items=0, boss_items=0, player_orders=0, boss_orders=0, ability_choices=0,
            shakes=0, player_shakes=0, boss_shakes=0, player_bursts=0, boss_bursts=0,
            passes=[0,0], strengths=[[0,0,0],[0,0,0]], risks=[[],[]],
            returned=[0,0], entry_tokens=self.tokens[0], entry_items=len(self.inv[0]), entry_abilities=len(self.owned), entry_boss_abilities=len(self.boss_owned),
            max_inventory=[len(bag) for bag in self.inv], turn_count=0, item_chain_max=0,
            abilities_acquired=[], bell_capacity_blocks=0, boss_combo_items=0,boss_combo_passes=0,boss_defenses_saved=0,
            safety_stacks_used=[0,0],stacked_safety_shakes=[0,0],zero_risk_shakes=[0,0],incoming_pressure_received=[0,0],
            risk_bins=[[[0,0,0.] for _ in range(12)] for _ in range(2)],
            strength_outcomes=[[[0,0,0.,0] for _ in range(3)] for _ in range(2)],guaranteed_bursts=[0,0])
        self.records.append(self.rec)

    def modifiers(self, who):
        stamp, safe, spicy, chilled, incoming, penalty = self.fx[who]
        r = self.rules
        rework = r.get('ability_rework',False)
        pressure = incoming - (r['CoolingReduction'] + (self.has(who, COOLING) and not rework) if chilled else 0)
        passive = r['LowHealthProbabilityReduction'] if rework and self.has(who,COOLING) and self.hp[who]<=r['LowHealthThreshold'] else 0
        if self.has(who,CRISIS) and self.hp[who]<=r['LowHealthThreshold']:passive+=r['CrisisProbabilityReduction']
        savings = r['SavingsRewardBonus'] if rework and self.has(who,HOTDEAL) and self.tokens[who]>=r['SavingsTokenThreshold'] else 0
        return (pressure, 1 + (r['HotDamageBonus'] if self.hot else 0),
                (r['StampRewardBonus'] if stamp else 0) + savings,
                safe * r['SafetyProbabilityReduction'] + passive,
                spicy * r['SpicyProbabilityIncrease'], penalty)

    def predict(self, strength, pressure, mods, limit=None):
        limit = self.limit if limit is None else limit
        return pressure_prediction(self.rules['steps'][strength-1], pressure, limit,
            self.rules['exponent'], mods[0], mods[3], mods[4])

    def reward(self, who, strength, risk, burst, mods, passive=True):
        reward = 5 if burst else self.rules.get('safe_rewards',[1,2,3])[strength-1] + mods[2]
        penalty = mods[5] if self.rules.get('item_rework') and len(mods)>5 else 0
        if not passive or (who==1 and not self.rules.get('shared_abilities')):return max(0,reward-penalty)
        if burst:return max(0,reward + (2 if self.has(who,BREAKAGE) else 0) + (self.rules['AccidentTokenBonus'] if self.has(who,ACCIDENT) else 0)-penalty)
        reward += strength == 1 and self.has(who,LIGHT)
        reward += self.rules.get('HighAbilityReward',1) if strength==3 and self.has(who,BOLD) else 0
        if self.rules.get('ability_rework'):reward += self.rules['MediumAbilityReward'] if strength==2 and self.has(who,SALTY) else 0
        else:reward += mods[1]>1 and self.has(who,HOTDEAL)
        reward += self.rules.get('danger_bonus',2) if risk>=self.rules.get('danger_threshold',.75) and self.has(who,DANGER) else 0
        return max(0,reward-penalty)

    def expected_reward(self, who, strength, risk, mods, passive=True):
        return (1-risk)*self.reward(who,strength,risk,False,mods,passive) + risk*self.reward(who,strength,risk,True,mods,passive)

    def utilities(self, who, pressure=None, own=None, opponent=None, limit=None, passing=False):
        pressure = self.pressure if pressure is None else pressure
        limit = self.limit if limit is None else limit
        own = self.modifiers(who) if own is None else own
        opponent = self.modifiers(1-who) if opponent is None else opponent
        key = (who, pressure, tuple(own), tuple(opponent), limit, passing)
        if key in self._utility_cache: return self._utility_cache[key]
        enemy = 1-who
        cost = self.rules['cost'] if who == 1 and not self.rules.get('shared_abilities') else self.cost(who)
        cash = coin(self.tokens[who], cost)
        enemy_cash = coin(self.tokens[enemy], self.cost(enemy) if self.rules.get('shared_abilities') else self.rules['cost'])
        weight, enemy_weight = life(self.hp[who]), life(self.hp[enemy])
        def reply(p, rough=False):
            reply_mods = list(opponent)
            if rough:reply_mods[0]+=self.rules['RoughPressureBonus']
            reply_key=(who,p,tuple(reply_mods),limit)
            if reply_key in self._reply_cache:return self._reply_cache[reply_key]
            best_value, best_risk = -float('inf'), 0.
            for s in (1,2,3):
                _, risk = self.predict(s, p, reply_mods, limit)
                # C# boss assumes base rewards for the player's reply, without reading ability rewards.
                aware=who==1 and self.stage>=self.rules.get('tactics',{}).get('DefenseStage',999)
                value = enemy_cash*self.expected_reward(enemy,s,risk,reply_mods,passive=aware) - risk*opponent[1]*enemy_weight
                if value > best_value: best_value, best_risk = value, risk
            self._reply_cache[reply_key]=best_risk
            return best_risk
        if passing:
            result = reply(pressure)*opponent[1]*enemy_weight*.7
            self._utility_cache[key] = result
            return result
        result = []
        for strength in (1,2,3):
            after, risk = self.predict(strength, pressure, own, limit)
            result.append(cash*self.expected_reward(who,strength,risk,own) - risk*own[1]*weight +
                (1-risk)*reply(after,strength==3 and self.has(who,ROUGH))*opponent[1]*enemy_weight*.7)
        self._utility_cache[key] = result
        return result

    def best(self, who, **kwargs):
        values = self.utilities(who, **kwargs)
        if who == 0 and self.policy == 'cautious': return values[0]
        if who == 0 and self.policy == 'greedy': return values[2]
        if who == 0 and self.policy == 'medium': return values[1]
        if who == 0 and self.policy == 'random': return sum(values)/3
        return max(values)

    def strength(self, who):
        rng = self.choices[who]
        if who == 0 and self.policy == 'cautious': return 1
        if who == 0 and self.policy == 'greedy': return 3
        if who == 0 and self.policy == 'medium': return 2
        if who == 0 and self.policy == 'random': return rng.randint(1,3)
        values = self.utilities(who)
        if who == 0 and self.policy == 'precise':
            return max(range(3),key=lambda i:values[i])+1
        later = max(0, self.stage-3)
        temperature = .14 if self.stage == 1 else .10 if self.stage == 2 else max(.04,.065-.004*later)
        exploration = .06 if self.stage == 1 else max(.015,.025-.001*later)
        tactics=self.rules.get('tactics',{})
        if who==1 and self.stage>=tactics.get('DefenseStage',999):
            temperature,exploration=tactics['StageFiveTemperature'],tactics['StageFiveExploration']
        elif who==1 and self.stage>=tactics.get('AttackStage',999):
            temperature,exploration=tactics['StageFourTemperature'],tactics['StageFourExploration']
        if who == 0: temperature, exploration = .065, .025
        best = max(values)
        weights = [math.exp((v-best)/temperature)+exploration for v in values]
        ticket = rng.random()*sum(weights)
        return 1 if ticket < weights[0] else 2 if ticket < sum(weights[:2]) else 3

    def usable(self, who, item):
        own = self.fx[who]
        enemy = 1-who
        if item == ICE: return self.pressure > 0
        if item in (CANDY,SALT): return self.rules.get('targeted_pressure',False) or self.pressure < self.limit
        if item == QUEUE: return self.rules.get('item_rework') or bool(self.inv[enemy])
        if item == SAFETY: return self.rules.get('safety_stacking',False) or not own[1]
        if item == PIZZA: return self.hp[who] < self.maxhp[who]
        if item == HOT: return not self.hot
        if item == STAMP: return not own[0]
        if item == OPENER: return self.limit > 1
        if item == STRAW: return self.rules.get('item_rework') or self.tokens[enemy] > 0 and self.tokens[who] < 9999
        if item == SODA: return self.pressure != self.limit//2
        if item == COIN: return self.tokens[who] < 9999
        if item == SWAP: return self.rules.get('item_rework') or any(a != b for a in self.inv[who] if a != SWAP for b in self.inv[enemy])
        if item == COOL: return not own[3]
        if item == PUNCH: return self.rules.get('item_rework') or self.tokens[enemy] > 0
        if item == LID: return self.limit < 999
        if item == EXCHANGE: return self.rules.get('item_rework') or len(self.inv[who]) > 1
        return True

    def held_value(self, who, item):
        if item == PIZZA: return 1.2 if self.hp[who] < self.maxhp[who] else .15
        if item == SAFETY: return 1.1 if self.modifiers(who)[4] > 0 else .65
        if item == BILL: return 1.3 if self.pressure > self.limit/2 else .5
        return .8

    def should_save_defense(self,who,item,strength):
        t=self.rules.get('tactics',{})
        if who!=1 or self.stage<t.get('DefenseStage',999) or item not in (ICE,SAFETY,COOL):return False
        mods=list(self.modifiers(who));pressure=self.pressure
        _,risk=self.predict(strength,pressure,mods)
        if item==ICE:pressure=max(0,pressure-self.rules['IceReduction'])
        elif item==SAFETY:mods[3]+=self.rules['SafetyProbabilityReduction']
        else:mods[0]-=self.rules['CoolingReduction']
        _,after=self.predict(strength,pressure,mods)
        return risk<t['DefenseRiskThreshold'] or risk-after<t['MinimumDefenseReduction']

    def combo_item(self,who):
        t=self.rules.get('tactics',{})
        if who!=1 or self.stage<t.get('AttackStage',999) or BILL not in self.inv[who]:return -1
        before=self.best(who);pass_now=self.utilities(who,passing=True)
        selected=-1;best=t['MinimumAttackImprovement']
        for i,item in enumerate(self.inv[who]):
            if item not in (CANDY,SALT,OPENER,SPICY) or not self.usable(who,item):continue
            pressure,limit=self.pressure,self.limit;opponent=list(self.modifiers(1-who))
            if item in (CANDY,SALT):
                amount=self.rules['CandyIncrease'] if item==CANDY else self.rules['SaltIncrease']
                if self.rules.get('targeted_pressure'):opponent[0]+=amount
                else:pressure+=amount
            elif item==OPENER:limit=max(1,limit-self.rules['OpenerReduction'])
            else:opponent[4]+=self.rules['SpicyProbabilityIncrease']
            value=self.utilities(who,pressure=pressure,limit=limit,opponent=opponent,passing=True)
            improvement=value-pass_now
            if value-before<t['MinimumComboGain'] or improvement<=best:continue
            selected,best=i,improvement
        return selected

    def score(self, who, item, before):
        own, opponent = list(self.modifiers(who)), list(self.modifiers(1-who))
        r, enemy = self.rules, 1-who
        if item == PIZZA: return life(self.hp[who])*1.1
        if item == ICE: return self.best(who, pressure=max(0,self.pressure-r['IceReduction']))-before
        if item in (CANDY,SALT) and r.get('targeted_pressure'):
            opponent[0]+=r['CandyIncrease'] if item==CANDY else r['SaltIncrease']
            return self.best(who,opponent=opponent)-before
        if item in (CANDY,SALT,OPENER,SODA):
            pressure, limit = self.pressure, self.limit
            if item == CANDY: pressure += r['CandyIncrease']
            elif item == SALT: pressure += r['SaltIncrease'] + self.has(who,SALTY)
            elif item == OPENER: limit = max(1, limit-int(r['OpenerReduction']))
            else: pressure = limit//2
            value = self.best(who, pressure=pressure, limit=limit)-before
            if BILL in self.inv[who]:
                value = max(value, self.utilities(who, pressure=pressure, limit=limit, passing=True)-before+.03)
            return value
        if item == QUEUE: return (.10+min(3,len(self.inv[enemy]))*.035 if self.inv[enemy] else 0) + r['TicketTokens']*coin(self.tokens[who],self.cost(who)) if r.get('item_rework') else .10+min(3,len(self.inv[enemy]))*.035
        if item == BILL: return self.utilities(who, passing=True)-before
        if item == SPICY: opponent[4] += r['SpicyProbabilityIncrease']
        elif item == SAFETY: own[3] += r['SafetyProbabilityReduction']
        elif item == COOL: own[0] -= r['CoolingReduction'] + (self.has(who,COOLING) and not r.get('shared_abilities'))
        elif item == HOT: own[1] += r['HotDamageBonus']; opponent[1] += r['HotDamageBonus']
        elif item == STAMP: own[2] += r['StampRewardBonus']
        elif item == STRAW:
            if r.get('item_rework'):return r['StrawTokens']*coin(self.tokens[who],self.cost(who))+min(r['StrawTokens'],self.tokens[enemy])*coin(self.tokens[enemy],self.cost(enemy))*.5
            return min(r['StrawTokens'], self.tokens[enemy])*(coin(self.tokens[who],self.cost(who))+coin(self.tokens[enemy],self.cost(enemy) if r.get('shared_abilities') else r['cost'])*.5)
        elif item == COIN: return r['CoinCupTokens']*coin(self.tokens[who],self.cost(who))
        elif item == PUNCH:
            if r.get('item_rework'):
                opponent[5]+=r['PunchTokens']
                return self.best(who,opponent=opponent)-before+.08*min(3,r['PunchTokens'])
            return min(r['PunchTokens'],self.tokens[enemy])*coin(self.tokens[enemy],self.cost(enemy) if r.get('shared_abilities') else r['cost'])*.7
        elif item == LID: return self.best(who, limit=min(999,self.limit+int(r['LidIncrease'])))-before
        elif item == EXCHANGE:
            if r.get('item_rework'):return .13
            held = [self.held_value(who,i) for i in self.inv[who] if i != EXCHANGE]
            if not held: return 0
            weighted = [(r['weights'][self.stage-1][i],self.held_value(who,i)) for i in range(19)
                        if i != HOT or not self.hot_drawn and not self.hot and all(HOT not in b for b in self.inv)]
            total = sum(w for w,_ in weighted)
            return (sum(w*v for w,v in weighted)/total-st.mean(held))*.2+.02 if total else 0
        elif item == SWAP:
            if r.get('item_rework'):return .16+st.mean(self.held_value(who,i) for i in self.inv[enemy])*.12 if self.inv[enemy] else .14
            a = [self.held_value(who,i) for i in self.inv[who] if i != SWAP]
            b = [self.held_value(who,i) for i in self.inv[enemy]]
            return (st.mean(b)-st.mean(a))*.18+.025 if a and b else 0
        return self.best(who, own=own, opponent=opponent)-before

    def buy(self, who):
        count, cost = int(self.pack(who)), self.cost(who)
        if self.tokens[who] < cost: return False
        if who==0 and self.policy=='stockpile' and self.hp[who]>2 and self.tokens[who]-cost<self.rules.get('SavingsTokenThreshold',12):return False
        if self.capacity(who)-len(self.inv[who]) < count:
            if who == 0: self.rec['bell_capacity_blocks'] += 1
            return False
        if who==0 and self.has(who,PREPAID) and self.first_bottle_order[who]:self.ability_effects[PREPAID]['tokens']+=self.rules['PrepaidDiscount']
        self.first_bottle_order[who]=False
        self.tokens[who] -= cost
        self.inv[who].extend(self.draw(count))
        if who == 0:
            if DISCOUNT in self.owned: self.ability_effects[DISCOUNT]['tokens'] += 1
            if count > self.rules['pack']: self.ability_effects[SERVICE]['items'] += 1
            if len(self.inv[who]) > self.rules['capacity']: self.ability_effects[STORAGE]['orders_enabled'] += 1
            if self.has(who,SERVICE) and count>self.rules['pack']:self.service_ready=False
        if who==1 and self.has(who,SERVICE) and count>self.rules['pack']:self.boss_service_ready=False
        self.stage_orders[who] += 1
        self.rec['player_orders' if who == 0 else 'boss_orders'] += 1
        self.rec['auto'] += max(.92,.34+.045+.10*(count-1)+.46) if who == 0 else .8
        self.rec['max_inventory'][who] = max(self.rec['max_inventory'][who],len(self.inv[who]))
        return True

    def use(self, who, index):
        item = self.inv[who][index]
        r, enemy = self.rules, 1-who
        own = self.fx[who]
        replacement = None
        # Keep the used slot present until the effect resolves, just as TryUseSlot does.
        if item == ICE: self.pressure = max(0,self.pressure-int(r['IceReduction']))
        elif item == CANDY:
            if r.get('targeted_pressure'):self.fx[enemy][4]+=int(r['CandyIncrease'])
            else:self.pressure += int(r['CandyIncrease'])
        elif item == QUEUE:
            if self.inv[enemy]:self.inv[enemy].pop(self.drop.randrange(len(self.inv[enemy])))
            if r.get('item_rework'):self.tokens[who]=min(9999,self.tokens[who]+int(r['TicketTokens']))
        elif item == SPICY: self.fx[enemy][2] += 1
        elif item == SAFETY: own[1] += 1
        elif item == PIZZA: self.hp[who] += 1
        elif item == HOT: self.hot = self.hot_drawn = True
        elif item == STAMP: own[0] = True
        elif item == OPENER: self.limit = max(1,self.limit-int(r['OpenerReduction']))
        elif item == STRAW:
            amount = min(int(r['StrawTokens']),self.tokens[enemy],9999-self.tokens[who])
            self.tokens[who] = min(9999,self.tokens[who]+(int(r['StrawTokens']) if r.get('item_rework') else amount)); self.tokens[enemy] -= amount
        elif item == SODA: self.pressure = self.limit//2
        elif item == COIN: self.tokens[who] = min(9999,self.tokens[who]+int(r['CoinCupTokens']))
        elif item == SWAP:
            if r.get('item_rework'):
                replacement=self.inv[enemy].pop(self.drop.randrange(len(self.inv[enemy]))) if self.inv[enemy] else self.draw(1)[0]
            else:
                pairs=[(a,b) for a,x in enumerate(self.inv[who]) for b,y in enumerate(self.inv[enemy]) if a!=index and x!=y]
                a,b=self.drop.choice(pairs);self.inv[who][a],self.inv[enemy][b]=self.inv[enemy][b],self.inv[who][a]
        elif item == SALT:
            if r.get('targeted_pressure'):self.fx[enemy][4]+=int(r['SaltIncrease'])
            else:
                self.pressure += int(r['SaltIncrease']) + (self.has(who,SALTY) and not r.get('ability_rework'))
                if self.has(who,SALTY) and not r.get('ability_rework'): self.ability_effects[SALTY]['pressure_added'] += 1
        elif item == COOL: own[3] = True
        elif item == PUNCH:
            if r.get('item_rework'):self.fx[enemy][5]+=int(r['PunchTokens'])
            else:self.tokens[enemy]=max(0,self.tokens[enemy]-int(r['PunchTokens']))
        elif item == LID: self.limit = min(999,self.limit+int(r['LidIncrease']))
        elif item == EXCHANGE:
            if r.get('item_rework'):replacement=self.draw(1,EXCHANGE)[0]
            else:
                target=self.drop.choice([i for i in range(len(self.inv[who])) if i!=index])
                self.inv[who][target]=self.draw(1,self.inv[who][target])[0]
        self.inv[who].pop(index)
        if replacement is not None:self.inv[who].insert(min(index,len(self.inv[who])),replacement)
        self.item_used[(who,item)] += 1
        if self.has(who,RECYCLE):
            if who==0:self.recycling+=1
            else:self.boss_recycling+=1
            used = self.recycling if who==0 else self.boss_recycling
            if used % 3 == 0:
                self.tokens[who] = min(9999,self.tokens[who]+1)
                if who==0:self.ability_effects[RECYCLE]['tokens'] += 1
        self.rec['player_items' if who == 0 else 'boss_items'] += 1
        self.rec['auto'] += .68
        return item == BILL

    def try_return(self,who):
        if not self.rules.get('item_rework') or self.tokens[who]<self.cost(who) or len(self.inv[who])<self.capacity(who)-1:return False
        if not self.inv[who]:return False
        index=min(range(len(self.inv[who])),key=lambda i:self.held_value(who,self.inv[who][i]))
        if self.held_value(who,self.inv[who][index])>=.6:return False
        self.inv[who].pop(index);self.tokens[who]=min(9999,self.tokens[who]+int(self.rules['ReturnTokens']))
        self.rec['returned'][who]+=1;self.rec['auto']+=.55
        return True

    def choose_ability(self):
        self.pending = False
        pool = [i for i in range(len(self.rules['abilities'])) if i not in self.owned]
        choices = self.ability_rng.sample(pool, min(3,len(pool)))
        if self.picks == 0 and self.first is not None:
            choice = None if self.first == 'skip' else int(self.first)
        elif self.ability_mode == 'economy':
            priority = {DISCOUNT:10,LIGHT:9,RECYCLE:8,SERVICE:7,BREAKAGE:6,TIDY:5,
                        COOLING:4,STORAGE:3,BOLD:2.5,HOTDEAL:2,SALTY:1.5,DANGER:1,
                        SUPPLY:11,ROUGH:6,CRISIS:8.5,PREPAID:9.5,ACCIDENT:7.5}
            choice = max(choices,key=priority.get)
        else: choice = self.ability_rng.choice(choices)
        self.picks += 1
        if choice is not None:
            self.owned.add(choice)
            self.rec['abilities_acquired'].append(choice)
            self.ability_at_pick.append((self.stage,choice,self.stage_orders[0]))
            if choice == RECYCLE: self.recycling = 0
            if choice == SERVICE:self.service_ready=True
            if choice == TIDY and self.rules.get('coupon_on_acquisition'):
                self.pending_coupons+=1
                self.ability_effects[TIDY]['acquisition_grants_queued']+=1
        # Even the controlled skipped reward pays the same hypothetical decision time.
        self.rec['ability_choices'] += 1

    def turn(self):
        self.clear_evaluation()
        who, r = self.actor, self.rules
        if who==0 or r.get('shared_abilities'):self.flush_coupons()
        self.rec['turn_count'] += 1
        self.rec['player_turns' if who == 0 else 'boss_turns'] += 1
        if who == 1: self.rec['auto'] += r['boss_think']
        else:
            for ability in self.owned: self.ability_effects[ability]['turns_owned'] += 1
        ordered = False
        used = 0
        passed = False
        completing_combo=False
        self.strength(who)  # initial C# plan before item preparation
        for _ in range(150):
            planned_strength=self.strength(who)
            before = self.best(who)
            best, slot = .035, -1
            planned=self.combo_item(who)
            if planned>=0:
                slot=planned;completing_combo=True;self.rec['boss_combo_items']+=1
            elif completing_combo and BILL in self.inv[who]:
                slot=self.inv[who].index(BILL);self.rec['boss_combo_passes']+=1
            # Duplicate props use independent 0.8..1.2 score variation, as in the boss AI.
            cache = {}
            for i, item in enumerate(self.inv[who] if slot<0 else []):
                if not self.usable(who,item): continue
                if self.should_save_defense(who,item,planned_strength):
                    self.rec['boss_defenses_saved']+=1
                    continue
                if item not in cache: cache[item] = self.score(who,item,before)
                value = cache[item]*(.8+.4*self.choices[who].random())
                if value > best: best,slot = value,i
            if slot >= 0:
                passed = self.use(who,slot); used += 1
                if who==0 or r.get('shared_abilities'):self.flush_coupons()
                self.clear_evaluation()
                if passed: break
            elif (who == 0 or not ordered) and self.buy(who):
                ordered = True
                self.clear_evaluation()
            elif not ordered and self.try_return(who):self.clear_evaluation()
            else: break
        else: self.censored = True
        self.rec['item_chain_max'] = max(self.rec['item_chain_max'],used)
        if passed:
            self.rec['passes'][who] += 1
            self.actor = 1-who
            return
        strength = self.strength(who)
        mods = self.modifiers(who)
        after, risk = self.predict(strength,self.pressure,mods)
        burst = self.roll.random() < risk
        reward = self.reward(who,strength,risk,burst,mods)
        bucket=0 if risk==0 else 11 if risk==1 else min(10,1+int(risk*10))
        bins=self.rec['risk_bins'][who][bucket];bins[0]+=1;bins[1]+=burst;bins[2]+=risk
        outcomes=self.rec['strength_outcomes'][who][strength-1]
        outcomes[0]+=1;outcomes[1]+=burst;outcomes[2]+=risk;outcomes[3]+=reward
        self.rec['guaranteed_bursts'][who]+=burst and after>=self.limit
        if who == 0:
            if burst and self.has(who,ACCIDENT):self.ability_effects[ACCIDENT]['tokens']+=r['AccidentTokenBonus']
            if self.has(who,CRISIS) and self.hp[who]<=r['LowHealthThreshold']:
                _,without=self.predict(strength,self.pressure,(mods[0],mods[1],mods[2],mods[3]-r['CrisisProbabilityReduction'],mods[4]))
                self.ability_effects[CRISIS]['risk_reduction_pp']+=(without-risk)*100
                self.ability_effects[CRISIS]['protected_shakes']+=1
            if burst and BREAKAGE in self.owned: self.ability_effects[BREAKAGE]['tokens'] += 2
            if not burst:
                rework=r.get('ability_rework',False)
                savings=self.tokens[who]>=r.get('SavingsTokenThreshold',12) if rework else mods[1]>1
                for ability, amount, active in ((LIGHT,1,strength==1),(BOLD,r.get('HighAbilityReward',1),strength==3),
                        (SALTY,r.get('MediumAbilityReward',1),rework and strength==2),
                        (HOTDEAL,r.get('SavingsRewardBonus',1),savings),(DANGER,r.get('danger_bonus',2),risk>=r.get('danger_threshold',.75))):
                    if ability in self.owned and active: self.ability_effects[ability]['tokens'] += amount
            if r.get('ability_rework') and COOLING in self.owned and self.hp[who]<=r['LowHealthThreshold']:
                _,without=self.predict(strength,self.pressure,(mods[0],mods[1],mods[2],mods[3]-r['LowHealthProbabilityReduction'],mods[4]))
                self.ability_effects[COOLING]['risk_reduction_pp']+=(without-risk)*100
                self.ability_effects[COOLING]['protected_shakes']+=1
            elif not r.get('ability_rework') and self.fx[0][3] and COOLING in self.owned:
                saved = max(0,r['steps'][strength-1]-r['CoolingReduction'])-max(0,r['steps'][strength-1]-r['CoolingReduction']-1)
                self.ability_effects[COOLING]['pressure_saved'] += saved
                self.ability_effects[COOLING]['chilled_shakes'] += 1
        self.tokens[who] = min(9999,self.tokens[who]+int(reward))
        self.pressure = after
        self.rec['safety_stacks_used'][who]+=self.fx[who][1]
        self.rec['stacked_safety_shakes'][who]+=self.fx[who][1]>1
        self.rec['zero_risk_shakes'][who]+=risk==0
        self.rec['incoming_pressure_received'][who]+=self.fx[who][4]
        self.fx[who] = [False,0,0,False,0,0]
        self.rec['shakes'] += 1
        self.rec['player_shakes' if who == 0 else 'boss_shakes'] += 1
        self.rec['strengths'][who][strength-1] += 1
        self.rec['risks'][who].append(risk)
        pause = .45+.30*min(1.,after/self.limit)
        duration = r['shake_duration']+pause
        if burst:
            self.hp[who] = max(0,self.hp[who]-int(mods[1]))
            self.bursts += 1
            self.rec['player_bursts' if who == 0 else 'boss_bursts'] += 1
            # Burst jumps the animation clock from ResultCheckTime to GripEndTime.
            duration = r['result_check']+pause+.30+1.05+(r['shake_duration']-r['grip_end'])+.22+.38
            self.new_bottle()
            if self.ability_mode != 'none' and self.bursts % 3 == 0 and len(self.owned) < len(r['abilities']) and self.hp[0] > 0 and not (self.hp[1] <= 0 and self.stage == 5):
                self.pending = True
        else:
            if strength==3 and self.has(who,ROUGH):
                self.fx[1-who][4]+=int(r['RoughPressureBonus'])
                if who==0:self.ability_effects[ROUGH]['pressure_added']+=int(r['RoughPressureBonus'])
            self.actor = 1-who
        self.rec['auto'] += duration
        assert min(self.tokens) >= 0 and max(self.tokens) <= 9999
        assert all(len(bag) <= self.capacity(i) for i,bag in enumerate(self.inv))

    def run(self, stop_first=False, stop_stage=False):
        while not self.finished:
            if self.pending:
                if stop_first and self.picks == 0: return 'checkpoint'
                self.choose_ability()
            if self.censored or self.rec['turn_count'] >= 1000:
                self.censored = self.rec['censored'] = True; self.finished = True; break
            if min(self.hp) <= 0:
                self.rec['won'] = self.hp[1] <= 0 and self.hp[0] > 0
                self.rec['end_tokens'] = self.tokens[0]
                self.rec['end_hp'] = self.hp[:]
                self.rec['end_abilities'] = len(self.owned)
                if not self.rec['won']: self.finished = True; break
                if self.stage == 5: self.completed = self.finished = True; break
                if stop_stage: self.finished = True; break
                self.begin_stage()
            self.turn()
        return 'complete' if self.completed else 'censored' if self.censored else 'lost'


def minutes(record, thinking=4):
    return (record['auto'] + thinking*record['player_turns'] + 1.5*record['player_items'] +
            record['player_orders'] + 6*record['ability_choices'])/60


def describe(values):
    if not values: return None
    values = sorted(values)
    def q(p):
        at = (len(values)-1)*p
        lower = int(at); upper = min(lower+1,len(values)-1)
        return values[lower]*(upper-at)+values[upper]*(at-lower) if upper != lower else values[lower]
    return dict(n=len(values),mean=st.mean(values),p10=q(.1),p25=q(.25),median=q(.5),p75=q(.75),p90=q(.9))


def wilson(wins,n):
    if not n: return [0.,0.]
    z=1.96; p=wins/n; den=1+z*z/n
    mid=(p+z*z/(2*n))/den; d=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/den
    return [100*(mid-d),100*(mid+d)]


def aggregate(games):
    n=len(games)
    ability_count=len(games[0].rules['abilities'])
    stages=[]
    for stage in range(1,6):
        records=[g.records[stage-1] for g in games if len(g.records)>=stage]
        wins=sum(rec['won'] for rec in records)
        row=dict(stage=stage,reached=len(records),won=wins,lost=sum(not rec['won'] and not rec['censored'] for rec in records),
            reach_pct=100*len(records)/n,win_pct=100*wins/len(records) if records else 0,win_ci95=wilson(wins,len(records)))
        for think in (0,2,4,8): row[f'minutes_think{think}']=describe([minutes(rec,think) for rec in records])
        row['automatic_minutes']=describe([rec['auto']/60 for rec in records])
        row['won_minutes']=describe([minutes(rec) for rec in records if rec['won']])
        for key in ('shakes','player_bursts','boss_bursts','player_orders','boss_orders','player_items','boss_items',
                    'entry_tokens','entry_items','entry_abilities','entry_boss_abilities','item_chain_max','bell_capacity_blocks',
                    'boss_combo_items','boss_combo_passes','boss_defenses_saved'):
            row[key]=describe([rec[key] for rec in records])
        for key in ('safety_stacks_used','stacked_safety_shakes','zero_risk_shakes','incoming_pressure_received','guaranteed_bursts','returned'):
            row[key]=[describe([rec[key][actor] for rec in records]) for actor in (0,1)]
        row['strength_outcomes']=[[[sum(rec['strength_outcomes'][actor][s][j] for rec in records) for j in range(4)] for s in range(3)] for actor in (0,1)]
        row['risk_bins']=[[[sum(rec['risk_bins'][actor][bucket][j] for rec in records) for j in range(3)] for bucket in range(12)] for actor in (0,1)]
        row['under5_pct']=100*sum(minutes(rec)<5 for rec in records)/len(records) if records else 0
        row['over8_pct']=100*sum(minutes(rec)>8 for rec in records)/len(records) if records else 0
        row['player_strength_pct']=[100*sum(rec['strengths'][0][s] for rec in records)/max(1,sum(rec['player_shakes'] for rec in records)) for s in range(3)]
        row['arrival_minutes']=describe([sum(minutes(rec) for rec in g.records[:stage-1]) for g in games if len(g.records)>=stage])
        stages.append(row)
    effects={i:Counter() for i in range(ability_count)}
    owners=Counter(); used=Counter(); generated=Counter(); picks=Counter()
    for game in games:
        owners.update(game.owned); used.update(game.item_used); generated.update(game.item_generated)
        picks.update(len(game.owned) for _ in range(1))
        for i in range(ability_count): effects[i].update(game.ability_effects[i])
    return dict(n=n,completed=sum(g.completed for g in games),completion_pct=100*sum(g.completed for g in games)/n,
        completion_ci95=wilson(sum(g.completed for g in games),n),censored=sum(g.censored for g in games),
        stages=stages,total_minutes=describe([sum(minutes(rec) for rec in g.records) for g in games]),
        completed_minutes=describe([sum(minutes(rec) for rec in g.records) for g in games if g.completed]),
        ability_count=describe([len(g.owned) for g in games]),ability_count_hist=dict(picks),
        effects={str(i):dict(owners=owners[i],**effects[i]) for i in range(ability_count)},
        item_stats={str(i):dict(generated=generated[i],player_used=used[0,i],boss_used=used[1,i],
            used_pct=100*(used[0,i]+used[1,i])/max(1,generated[i])) for i in range(19)},
        max_inventory=max(max(rec['max_inventory']) for g in games for rec in g.records),
        pending_coupons_at_end=sum(g.pending_coupons for g in games),
        acquisition_stats={str(i):dict(picks=sum(a==i for g in games for _,a,_ in g.ability_at_pick),
            final_stage_picks=sum(a==i and stage==5 for g in games for stage,a,_ in g.ability_at_pick),
            after_first_order=sum(a==i and orders>0 for g in games for _,a,orders in g.ability_at_pick)) for i in range(ability_count)})


def batch(n, rules, seed, policy='balanced', mode='random'):
    games=[]
    for i in range(n):
        game=Campaign(seed+i,rules,policy,mode);game.run();games.append(game)
        if (i+1)%1000 == 0: print(f'  {policy}/{mode}: {i+1}/{n}',flush=True)
    return games


def counterfactual(n,rules,seed):
    # The same first-choice states are copied for every intervention. No conditioning
    # on subsequently winning, selecting, or surviving with a particular ability.
    checkpoints=[]; eligible=0
    for i in range(n):
        game=Campaign(seed+i,rules)
        if game.run(stop_first=True)=='checkpoint': checkpoints.append(game)
    eligible=len(checkpoints)
    controls=[]; rows=[]
    for state in checkpoints:
        g=copy.deepcopy(state);g.first='skip';g.run();controls.append(g.completed)
    control_wins=sum(controls)
    for ability in range(len(rules['abilities'])):
        wins=[]; total_minutes=[]; effects=Counter(); reached=[0]*5
        for state in checkpoints:
            g=copy.deepcopy(state);g.first=ability;g.run();wins.append(g.completed)
            total_minutes.append(sum(minutes(rec) for rec in g.records))
            effects.update(g.ability_effects[ability])
            for j in range(len(g.records)):reached[j]+=1
        differences=[int(a)-int(b) for a,b in zip(wins,controls)]
        delta=st.mean(differences)*100 if differences else 0
        se=st.stdev(differences)/math.sqrt(eligible)*100 if eligible>1 else 0
        rows.append(dict(ability=ability,n=eligible,wins=sum(wins),completion_pct=100*sum(wins)/max(1,eligible),
            control_pct=100*control_wins/max(1,eligible),delta_pp=delta,delta_ci95=[delta-1.96*se,delta+1.96*se],
            minutes=describe(total_minutes),effects=dict(effects),reached=reached))
        print(f'  first ability {ability}: {sum(wins)}/{eligible}, delta {delta:+.2f} pp',flush=True)
    return dict(attempts=n,eligible=eligible,control_completed=control_wins,abilities=rows,
        design='Paired copies at the first legitimate ability checkpoint: force this ability versus skip that one reward; later choices random in both branches.')


def diagnostics(n, rules, seed):
    output={'kind':'offline_diagnostics_not_unity_play','seed':seed,'n_per_stage':n,'isolated_stages':[]}
    for stage in range(1,6):
        games=[]
        for i in range(n):
            g=Campaign(seed+i,rules,ability_mode='none',start_stage=stage)
            g.run(stop_stage=True);games.append(g)
        records=[g.records[0] for g in games];wins=sum(r['won'] for r in records)
        output['isolated_stages'].append(dict(stage=stage,n=n,wins=wins,win_pct=100*wins/n,
            ci95=wilson(wins,n),minutes=describe([minutes(r) for r in records]),
            censored=sum(g.censored for g in games)))
        print(f'isolated stage {stage}: {wins}/{n}',flush=True)
    a=[];b=[]
    for i in range(n):
        for actor,values in ((0,a),(1,b)):
            g=Campaign(seed+100000+i,rules,ability_mode='none',start_stage=3)
            g.actor=actor;g.run(stop_stage=True);values.append(g.records[0]['won'])
    diff=[int(x)-int(y) for x,y in zip(a,b)];delta=st.mean(diff)*100
    se=st.stdev(diff)/math.sqrt(n)*100
    output['first_actor_stage3']=dict(n=n,player_starts_pct=100*sum(a)/n,boss_starts_pct=100*sum(b)/n,
        delta_pp=delta,delta_ci95=[delta-1.96*se,delta+1.96*se])
    picks=Counter();late=Counter()
    probe=max(500,n//2)
    for i in range(probe):
        g=Campaign(seed+200000+i,rules);g.run()
        for stage,ability,orders in g.ability_at_pick:
            picks[ability]+=1
            if ability==TIDY and stage==5:late['tidy_stage5']+=1
            if ability==SERVICE and orders>0:late['service_after_first_order']+=1
    output['late_choice_probe']=dict(n=probe,tidy_picks=picks[TIDY],tidy_stage5=late['tidy_stage5'],
        service_picks=picks[SERVICE],service_after_first_order=late['service_after_first_order'])
    (DEST/'diagnostics.json').write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')


def simulation_job(job):
    label,rules,n,seed,policy,mode,stage,secondary=job
    games=[]
    for i in range(n):
        game=Campaign(seed+i,rules,policy,mode,start_stage=stage or 1)
        game.run(stop_stage=bool(stage));games.append(game)
        if (i+1)%2000==0:print(f'{label}: {i+1}/{n}',flush=True)
    if stage:
        wins=sum(g.records[0]['won'] for g in games)
        return label,dict(stage=stage,n=n,wins=wins,win_pct=100*wins/n,ci95=wilson(wins,n),
            minutes=describe([minutes(g.records[0]) for g in games]),censored=sum(g.censored for g in games))
    result=aggregate(games)
    result['_outcomes']=[int(g.completed) for g in games]
    return label,result


def paired_difference(a,b):
    n=min(len(a),len(b));diff=[y-x for x,y in zip(a[:n],b[:n])]
    delta=st.mean(diff)*100;se=st.stdev(diff)/math.sqrt(n)*100 if n>1 else 0
    return dict(n=n,delta_pp=delta,ci95=[delta-1.96*se,delta+1.96*se],
        improved=sum(x==1 for x in diff),worsened=sum(x==-1 for x in diff))


def run_rebalance(args,rules):
    started=time.perf_counter()
    path=DEST/'comparison.json';comparison=json.loads(path.read_text(encoding='utf-8'))
    before=comparison['before'];frozen=before['rules'];seed=before['seed']
    cases=[('abilities','balanced','random',args.runs),('no_abilities','balanced','none',args.runs),
        ('economy_choices','balanced','economy',args.secondary),('always_1','cautious','random',args.secondary),
        ('always_2','medium','random',args.secondary),('always_3','greedy','random',args.secondary),
        ('random_strength','random','random',args.secondary),('precise_adaptive','precise','random',args.secondary)]
    jobs=[(label,rules,n,seed,policy,mode,None,args.secondary) for label,policy,mode,n in cases]
    jobs.extend([('baseline_replay',frozen,args.runs,seed,'balanced','random',None,args.secondary),
        ('baseline_always_2',frozen,args.secondary,seed,'medium','random',None,args.secondary)])
    rewards_only=copy.deepcopy(frozen);rewards_only['safe_rewards']=rules['safe_rewards']
    abilities_only=copy.deepcopy(rules);abilities_only['tactics']={}
    jobs.extend([('rewards_only',rewards_only,args.secondary,seed,'balanced','random',None,args.secondary),
        ('rewards_and_abilities',abilities_only,args.secondary,seed,'balanced','random',None,args.secondary)])
    if args.isolated:
        jobs.extend((f'isolated_{stage}',rules,args.isolated,seed+2000000,'balanced','none',stage,args.secondary) for stage in range(1,6))
    rows={}
    with ProcessPoolExecutor(max_workers=args.workers) as executor:
        futures=[executor.submit(simulation_job,j) for j in jobs]
        for future in as_completed(futures):
            label,row=future.result();rows[label]=row
            print(label,round(row.get('completion_pct',row.get('win_pct',0)),3),flush=True)
    paired={label:paired_difference(rows['baseline_replay']['_outcomes'],rows[label]['_outcomes']) for label in
        ('abilities','rewards_only','rewards_and_abilities')}
    paired['brain_increment']=paired_difference(rows['rewards_and_abilities']['_outcomes'],rows['abilities']['_outcomes'])
    if args.runs==before['cases']['abilities']['n']:
        replay=rows['baseline_replay'];original=before['cases']['abilities']
        matched=replay['completed']==original['completed'] and [s['reached'] for s in replay['stages']]==[s['reached'] for s in original['stages']]
        if not matched:raise RuntimeError('Frozen baseline replay changed; investigate before reporting a before/after comparison.')
    else:matched=None
    for row in rows.values():row.pop('_outcomes',None)
    after=dict(kind='offline_rebalance_model_not_unity_play',created_utc=datetime.now(timezone.utc).isoformat(),
        seed=seed,rules=rules,assumptions=before['assumptions'],cases={label:rows[label] for label,_,_,_ in cases},
        baseline_replay=rows['baseline_replay'],baseline_replay_matched=matched,
        ablations={k:rows[k] for k in ('rewards_only','rewards_and_abilities')},
        isolated_stages=[rows[f'isolated_{stage}'] for stage in range(1,6)] if args.isolated else [],
        paired_comparisons=paired,elapsed_seconds=time.perf_counter()-started)
    after['assumptions']=dict(after['assumptions'],boss='Stages 4/5 commit attack-plus-bill combos; stage 5 conserves meaningful defense and reads public player reward abilities.',
        coupon='Acquisition and stage-start coupon grants wait for free slots outside busy presentation.')
    comparison['baseline_always_2']=rows['baseline_always_2']
    comparison['current_revision']=rules['revision']
    (DEST/'results.json').write_text(json.dumps(after,ensure_ascii=False,indent=2),encoding='utf-8')
    path.write_text(json.dumps(comparison,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'Rebalance results saved; {after["elapsed_seconds"]:.1f}s. Baseline reproduced: {matched}',flush=True)


def design_probability_review(rules):
    """Enumerate public pressure states; these are design calculations, not Play checks."""
    rows=[]
    for pressure in (0,6,12,18,24):
        for incoming,caps,chilled in ((0,0,False),(0,1,False),(0,2,False),(6,0,False),(6,1,False),(6,1,True)):
            for strength in (1,2,3):
                after,risk=pressure_prediction(rules['steps'][strength-1],pressure,30,rules['exponent'],
                    incoming-(rules['CoolingReduction'] if chilled else 0),caps*rules['SafetyProbabilityReduction'],0)
                reply_pressure,reply=pressure_prediction(rules['steps'][0],after,30,rules['exponent'],0,0,0)
                rows.append(dict(pressure=pressure,limit=30,strength=strength,incoming=incoming,caps=caps,chilled=chilled,
                    after=after,risk_pct=risk*100,opponent_low_risk_pct=reply*100,
                    safe_tokens=rules['safe_rewards'][strength-1],safe_income_expectation=(1-risk)*rules['safe_rewards'][strength-1]))
    attack=[]
    for pressure in (0,6,12,18,24):
        for amount in (rules['SaltIncrease'],rules['CandyIncrease']):
            # Same own action with/without a pending attack; then the opponent chooses 1.
            after,own=pressure_prediction(rules['steps'][1],pressure,30,rules['exponent'],0,0,0)
            _,before=pressure_prediction(rules['steps'][0],after,30,rules['exponent'],0,0,0)
            _,after_attack=pressure_prediction(rules['steps'][0],after,30,rules['exponent'],amount,0,0)
            attack.append(dict(pressure=pressure,amount=amount,own_risk_pct=own*100,
                target_risk_before_pct=before*100,target_risk_after_pct=after_attack*100,
                target_delta_pp=(after_attack-before)*100))
    return dict(formula='min(1,max(0,(pressure_after/limit)^exponent + spicy_stacks*0.15 - safety_stacks*0.15 - passive)); pressure_after>=limit or W overrides to 1',
        roll='Fresh independent uniform roll per committed shake; previews never draw RNG. No hidden pity or boss bonus.',
        rows=rows,attack_examples=attack,
        caveat='Opponent reply examples use strength 1 and no defensive response; actual campaigns allow defense and other strengths.')


def run_design_rebalance(args,rules):
    started=time.perf_counter()
    path=DEST/'comparison.json';comparison=json.loads(path.read_text(encoding='utf-8'))
    if 'design_before' not in comparison:
        previous=json.loads((DEST/'results.json').read_text(encoding='utf-8'))
        if previous['rules']['revision']>=4:raise RuntimeError('Missing pre-design baseline.')
        comparison['design_before']=previous
    before=comparison['design_before'];frozen=before['rules'];seed=before['seed']
    if not args.design_pilot and 'revision4_before' not in comparison:
        previous=json.loads((DEST/'results.json').read_text(encoding='utf-8'))
        if previous['rules']['revision']==4:comparison['revision4_before']=previous
    if args.design_pilot:
        candidates={}
        for candy,salt in ((6,3),(4,2),(5,2)):
            variant=copy.deepcopy(rules);variant['CandyIncrease']=candy;variant['SaltIncrease']=salt
            candidates[f'attack_{candy}_{salt}']=variant
        rows={}
        with ProcessPoolExecutor(max_workers=args.workers) as executor:
            futures=[executor.submit(simulation_job,(label,variant,args.runs,seed,'balanced','random',None,0)) for label,variant in candidates.items()]
            for future in as_completed(futures):
                label,row=future.result();row.pop('_outcomes',None);rows[label]=row
                print(label,row['completion_pct'],[round(s['minutes_think4']['mean'],2) for s in row['stages']],
                    [round(s['player_strength_pct'][1],1) for s in row['stages']],flush=True)
        comparison['design_trials']=dict(n=args.runs,seed=seed,cases=rows)
        path.write_text(json.dumps(comparison,ensure_ascii=False,indent=2),encoding='utf-8')
        return
    cases=[('abilities','balanced','random',args.runs),('no_abilities','balanced','none',args.runs),
        ('economy_choices','balanced','economy',args.secondary),('always_1','cautious','random',args.secondary),
        ('always_2','medium','random',args.secondary),('always_3','greedy','random',args.secondary),
        ('precise_adaptive','precise','random',args.secondary),('stockpile','stockpile','random',args.secondary),
        ('random_strength','random','random',args.secondary)]
    jobs=[(label,rules,n,seed,policy,mode,None,0) for label,policy,mode,n in cases]
    jobs.append(('baseline_replay',frozen,args.runs,seed,'balanced','random',None,0))
    previous=comparison.get('revision4_before')
    if previous:
        old=previous['rules']
        jobs.append(('previous_design_replay',old,args.runs,seed,'balanced','random',None,0))
        high=copy.deepcopy(old);high['HighAbilityReward']=rules['HighAbilityReward']
        resources=copy.deepcopy(old)
        resources.update(boss_start_items=rules['boss_start_items'],boss_start_tokens=rules['boss_start_tokens'])
        jobs.extend((label,variant,args.secondary,seed,'balanced','random',None,0) for label,variant in
            (('high_bonus_only',high),('boss_resources_only',resources)))
    stacking=copy.deepcopy(frozen);stacking['safety_stacking']=True
    medium=copy.deepcopy(stacking);medium['steps']=rules['steps']
    attack=copy.deepcopy(medium);attack.update(targeted_pressure=True,CandyIncrease=rules['CandyIncrease'],SaltIncrease=rules['SaltIncrease'])
    jobs.extend((label,variant,args.secondary,seed,'balanced','random',None,0) for label,variant in
        (('stacking_only',stacking),('stacking_and_medium',medium),('without_ability_rework',attack)))
    if args.isolated:
        jobs.extend((f'isolated_{stage}',rules,args.isolated,seed+2000000,'balanced','none',stage,0) for stage in range(1,6))
    rows={};experiments=None
    with ProcessPoolExecutor(max_workers=args.workers) as executor:
        futures={executor.submit(simulation_job,job):job[0] for job in jobs}
        if args.ability_runs:futures[executor.submit(counterfactual,args.ability_runs,rules,seed+4000000)]='ability_experiment'
        for future in as_completed(futures):
            label=futures[future]
            if label=='ability_experiment':experiments=future.result();continue
            _,row=future.result();rows[label]=row
            print(label,round(row.get('completion_pct',row.get('win_pct',0)),3),flush=True)
    replay=rows['baseline_replay'];matched=None
    if args.runs==before['cases']['abilities']['n']:
        original=before['cases']['abilities']
        matched=replay['completed']==original['completed'] and [s['reached'] for s in replay['stages']]==[s['reached'] for s in original['stages']]
        if not matched:raise RuntimeError('Pre-design baseline did not reproduce; do not report a causal comparison.')
    comparisons={label:paired_difference(replay['_outcomes'],rows[label]['_outcomes']) for label in
        ('abilities','stacking_only','stacking_and_medium','without_ability_rework')}
    previous_matched=None
    if previous:
        prior=rows['previous_design_replay']
        if args.runs==previous['cases']['abilities']['n']:
            original=previous['cases']['abilities']
            previous_matched=prior['completed']==original['completed'] and [s['reached'] for s in prior['stages']]==[s['reached'] for s in original['stages']]
            if not previous_matched:raise RuntimeError('Revision 4 baseline did not reproduce.')
        comparisons.update({key:paired_difference(prior['_outcomes'],rows[label]['_outcomes']) for key,label in
            (('from_previous_design','abilities'),('high_bonus_only','high_bonus_only'),('boss_resources_only','boss_resources_only'))})
    for row in rows.values():row.pop('_outcomes',None)
    output=dict(kind='offline_design_rebalance_not_unity_play',created_utc=datetime.now(timezone.utc).isoformat(),
        seed=seed,rules=rules,baseline_revision=frozen['revision'],baseline_replay_matched=matched,
        assumptions=before['assumptions'],cases={label:rows[label] for label,_,_,_ in cases},
        baseline_replay=replay,ablations={label:rows[label] for label in ('stacking_only','stacking_and_medium','without_ability_rework')},
        isolated_stages=[rows[f'isolated_{stage}'] for stage in range(1,6)] if args.isolated else [],
        paired_comparisons=comparisons,ability_experiment=experiments,probability_review=design_probability_review(rules),
        elapsed_seconds=time.perf_counter()-started)
    if previous:
        output.update(previous_design_replay=rows['previous_design_replay'],previous_design_replay_matched=previous_matched,
            recent_ablations={label:rows[label] for label in ('high_bonus_only','boss_resources_only')})
    output['assumptions']=dict(output['assumptions'],
        pressure_attacks='Target next-shake pressure; pending through passes/replacement bottles until target shakes; reset at stage transition.',
        stockpile='Player holds 12 tokens after ordering while HP>2; otherwise same short-horizon policy. This is one simple reserve policy, not optimal saving.',
        ability_rework='Middle safe +1; HP<=2 risk -5pp; 12 tokens at commitment safe +1. No item ownership requirement.')
    comparison['current_revision']=rules['revision']
    (DEST/'results.json').write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')
    path.write_text(json.dumps(comparison,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'Design rebalance saved; {output["elapsed_seconds"]:.1f}s. Baseline reproduced: {matched}',flush=True)


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    parser=argparse.ArgumentParser()
    parser.add_argument('--runs',type=int,default=10000)
    parser.add_argument('--secondary',type=int,default=2500)
    parser.add_argument('--ability-runs',type=int,default=2000)
    parser.add_argument('--seed',type=int,default=202610200)
    parser.add_argument('--diagnostics-only',type=int,default=0)
    parser.add_argument('--precision-only',type=int,default=0)
    parser.add_argument('--rebalance',action='store_true')
    parser.add_argument('--design-rebalance',action='store_true')
    parser.add_argument('--design-pilot',action='store_true')
    parser.add_argument('--workers',type=int,default=3)
    parser.add_argument('--isolated',type=int,default=2000)
    args=parser.parse_args()
    started=time.perf_counter();rules=load_rules();DEST.mkdir(parents=True,exist_ok=True)
    if args.design_rebalance or args.design_pilot:
        run_design_rebalance(args,rules)
        return
    if args.rebalance:
        run_rebalance(args,rules)
        return
    if args.precision_only:
        path=DEST/'diagnostics.json'
        diagnostic=json.loads(path.read_text(encoding='utf-8')) if path.exists() else {}
        diagnostic['precise_policy']=aggregate(batch(args.precision_only,rules,args.seed,'precise','random'))
        path.write_text(json.dumps(diagnostic,ensure_ascii=False,indent=2),encoding='utf-8')
        print('precise policy', diagnostic['precise_policy']['completion_pct'],flush=True)
        return
    if args.diagnostics_only:
        diagnostics(args.diagnostics_only,rules,args.seed+2000000)
        return
    output=dict(kind='offline_campaign_model_not_unity_play',created_utc=datetime.now(timezone.utc).isoformat(),
        seed=args.seed,rules=rules,assumptions=dict(thinking_seconds_per_player_turn=[0,2,4,8],
            default_thinking=4,extra_item_decision_seconds=1.5,extra_bell_decision_seconds=1,ability_selection_seconds=6,
            player_orders='Own-turn whenever affordable and room; no off-turn ordering or queue races modeled.',
            boss='C# public-state utility, stage temperature, item scoring, one purchase per turn.',
            player='Same public-state utility, passive-aware rewards, stage-independent temperature .065; not optimal human play.',
            ability_choices='Uniform among three uniformly sampled unowned abilities unless economy mode.',
            timing='Authored choreography plus modeled human delays; excludes frame overhead, pauses, menus/reading outside decisions and clicking next stage.'),
        cases={})
    for label, policy, mode, count in [('abilities','balanced','random',args.runs),('no_abilities','balanced','none',args.runs),
            ('economy_choices','balanced','economy',args.secondary),('always_1','cautious','random',args.secondary),
            ('always_3','greedy','random',args.secondary),('random_strength','random','random',args.secondary)]:
        print(label,flush=True)
        output['cases'][label]=aggregate(batch(count,rules,args.seed,policy,mode))
        print(f"  completion {output['cases'][label]['completion_pct']:.2f}%",flush=True)
    if args.ability_runs: output['first_ability_experiment']=counterfactual(args.ability_runs,rules,args.seed+1000000)
    output['elapsed_seconds']=time.perf_counter()-started
    (DEST/'results.json').write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'Saved {DEST}/results.json; elapsed {output["elapsed_seconds"]:.1f}s',flush=True)


if __name__=='__main__':main()
