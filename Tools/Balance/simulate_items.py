"""Offline balance model. No Unity, compiler, OS input, screenshots or audio execution.

LegacyDuel preserves the rules immediately before the simplification for comparison.
SimpleDuel models the new one-effect items and reads the selected C# tuning in runtime mode.
"""
from __future__ import annotations
import argparse
from collections import Counter
from dataclasses import dataclass, asdict, replace
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import random
import re
import statistics
import time

ROOT=Path(__file__).resolve().parents[2]
DEST=ROOT/'output/verification/latest/item-balance'
NAMES=['얼음컵','발포 캔디','탄산 시럽','취소 번호표','계산서','매운맛 주문서',
       '안전 마개','피자 한 조각','핫소스','더블 스탬프','팁 봉투']
ICE,CANDY,SYRUP,QUEUE,BILL,SPICY,SAFETY,PIZZA,HOT,STAMP,TIP=range(11)
SAFE,DOUBLE,TIPPED,SYRUPED,FORCED,SKIP=range(6)
# Frozen previous rules, not inferred from the newly tuned runtime.
RULES={'min':25,'max':35,'exponent':1.,'capacity':8,'cost':10,'pack':4,'step':2}

@dataclass(frozen=True)
class LegacyProfile:
    ice: int=3
    candy: int=4
    syrup: int=2
    safety: float=.30
    queue_self: bool=False
    stamp_burst: int=2
    stamp_tip: bool=True
    tip: int=3
    tip_threshold: float=.50
    pizza_pressure: int=0
    pizza_cost: int=0
    hot_opponent: bool=False


class LegacyDuel:
    def __init__(self, seed, profile, policies=("balanced","balanced"), caps=(2,2),
                 hp=(3,3), tokens=(0,0), starter=None, hero=0):
        self.cfg,self.rules=profile,RULES
        self.drop=random.Random(seed^0x28D7)
        self.roll=random.Random(seed^0x915A)
        self.limit_rng=random.Random(seed^0x1911)
        self.choice=[random.Random(seed^0x719B),random.Random(seed^0x43A1)]
        self.hp=list(hp); self.maxhp=list(hp); self.tokens=list(tokens)
        self.inv=[self.drop.sample(range(11),2),self.drop.sample(range(11),2)]
        if starter is not None:
            other=self.inv[hero][1]
            if other==starter: other=(other+1+self.drop.randrange(10))%11
            self.inv[hero]=[starter,other]
        self.fx=[[False]*6,[False]*6]
        self.pressure=0; self.limit=0; self.hot=False; self.hot_target=1; self.actor=0
        self.policies,self.caps=policies,caps
        self.actions=self.turns=self.bursts=self.skips=self.passes=0
        self.buys=[0,0]; self.used=Counter(); self.acquired=Counter()
        for bag in self.inv: self.acquired.update(bag)
        self.earned=[0,0]; self.stamp_profit=0; self.tip_profit=0
        self.new_bottle()

    def new_bottle(self):
        low,high=self.rules["min"],self.rules["max"]
        value=self.limit_rng.randint(low,high)
        if value==self.limit and high>low: value=low+(value-low+1)%(high-low+1)
        self.limit=value; self.pressure=0; self.hot=False

    def risk(self, who, strength, pressure=None, safe=None):
        own=self.fx[who]
        after=(self.pressure if pressure is None else pressure)+strength*self.rules["step"]+(self.cfg.syrup if own[SYRUPED] else 0)
        if after>=self.limit: return 1.
        reduction=self.cfg.safety if (own[SAFE] if safe is None else safe) else 0.
        return max(0.,min(1.,(after/self.limit)**self.rules["exponent"]-reduction))

    def hp_weight(self, who):
        return 1.9 if self.hp[who]<=1 else 1.2 if self.hp[who]==2 else 1.

    def damage(self, who):
        return 2 if self.hot and (not self.cfg.hot_opponent or self.hot_target==who) else 1

    def coin_weight(self, who):
        return .12 if self.tokens[who]<10 else .065 if self.tokens[who]<20 else .025

    def reward(self, who, strength, risk, burst):
        own=self.fx[who]
        multiplier=self.cfg.stamp_burst if burst and own[DOUBLE] else 2 if own[DOUBLE] else 1
        base=5 if burst else strength
        tip=self.cfg.tip if not burst and own[TIPPED] and risk>=self.cfg.tip_threshold else 0
        return (base+tip)*multiplier if self.cfg.stamp_tip else base*multiplier+tip

    def strength(self, who):
        if self.fx[who][FORCED]: return 3
        policy=self.policies[who]
        if policy in ("random","live"): return self.choice[who].randint(1,3)
        if policy=="cautious": return 1
        if policy=="greedy": return 3
        values=[]
        for s in (1,2,3):
            p=self.risk(who,s)
            gain=(1-p)*self.reward(who,s,p,False)+p*self.reward(who,s,p,True)
            life=self.hp_weight(who)*self.damage(who)
            opp_force=3 if self.fx[1-who][FORCED] else 2
            after=self.pressure+s*self.rules["step"]+(self.cfg.syrup if self.fx[who][SYRUPED] else 0)
            opponent_risk=self.risk(1-who,opp_force,after)
            # One-step public-state estimate; this is a policy assumption, not a human optimal-play proof.
            score=self.coin_weight(who)*gain-p*life+(1-p)*opponent_risk*.48*self.hp_weight(1-who)
            score+=self.choice[who].uniform(-.012,.012)
            values.append(score)
        return max(range(3),key=values.__getitem__)+1

    def usable(self, who, item):
        own,other=self.fx[who],self.fx[1-who]
        if item==ICE: return self.pressure>0
        if item==SYRUP: return not other[SYRUPED]
        if item==QUEUE: return not (own if self.cfg.queue_self else other)[SKIP]
        if item==SPICY: return not other[FORCED]
        if item==SAFETY: return not own[SAFE]
        if item==PIZZA: return self.hp[who]<self.maxhp[who] and self.tokens[who]>=self.cfg.pizza_cost
        if item==HOT: return not self.hot
        if item==STAMP: return not own[DOUBLE]
        if item==TIP: return not own[TIPPED]
        return True

    def live_score(self, who, item, s):
        risk=self.risk(who,s); own,opp=self.fx[who],self.fx[1-who]
        if item==PIZZA: return 100+(self.maxhp[who]-self.hp[who])*10 if self.hp[who]<self.maxhp[who] else 0
        if item==ICE: return 25+risk*30 if self.pressure>=3 and risk>=.18 and max(0,self.pressure-self.cfg.ice)+s*2+(self.cfg.syrup if own[SYRUPED] else 0)<self.limit else 0
        if item==CANDY: return 40+risk*30 if risk>=.4 else 0
        if item==SYRUP: return 24 if not opp[SYRUPED] and risk<.55 else 0
        if item==QUEUE:
            if self.cfg.queue_self: return 30+risk*15 if not own[SKIP] and risk<.65 else 0
            return 20 if not opp[SKIP] and risk<.22 else 0
        if item==BILL: return 40+risk*35 if risk>=.55 else 0
        if item==SPICY: return 23 if not opp[FORCED] and risk<.55 else 0
        if item==SAFETY: return 60+risk*15 if not own[SAFE] and .25<=risk<1 else 0
        if item==HOT:
            if self.cfg.hot_opponent: return 26 if not self.hot and risk<.7 else 0
            return 28 if not self.hot and risk<.25 and self.hp[1-who]<=2 else 0
        if item==STAMP:
            if self.cfg.stamp_burst>1: return 25 if not own[DOUBLE] and risk>=.5 and self.hp[who]>self.damage(who) else 0
            return 18+s*4-risk*10 if not own[DOUBLE] and s>=2 and risk<.65 else 0
        if item==TIP: return 20 if not own[TIPPED] and self.cfg.tip_threshold<=risk<.8 else 0
        return 0

    def score(self, who, item, strength):
        if not self.usable(who,item): return -1.
        if self.policies[who]=="live": return self.live_score(who,item,strength)/100
        own,other=self.fx[who],self.fx[1-who]
        p=self.risk(who,strength); life=self.hp_weight(who)*self.damage(who)
        cash=self.coin_weight(who)
        opp_s=3 if other[FORCED] else 2
        enemy_risk=self.risk(1-who,opp_s)
        if item==ICE:
            reduction=p-self.risk(who,strength,max(0,self.pressure-self.cfg.ice))
            return reduction*life*.9-.025
        if item==SAFETY:
            return (p-self.risk(who,strength,safe=True))*life-.025
        if item in (BILL,CANDY):
            raised=self.pressure+(self.cfg.candy if item==CANDY else 0)
            attack=self.risk(1-who,opp_s,raised)-enemy_risk
            counter=.5 if BILL in self.inv[1-who] or CANDY in self.inv[1-who] else 1.
            gain=(1-p)*self.reward(who,strength,p,False)+p*self.reward(who,strength,p,True)
            return p*life+attack*.7*counter-cash*gain-.20
        if item==SYRUP:
            after=min(1.,enemy_risk+self.cfg.syrup/self.limit)
            return (after-enemy_risk)*self.hp_weight(1-who)-.020
        if item==SPICY:
            return max(0,self.risk(1-who,3)-self.risk(1-who,1))*.8-.02
        if item==QUEUE:
            if self.cfg.queue_self:
                future=min(1.,(self.pressure+(strength+opp_s+1)*2)/self.limit)
                return future*.45*self.hp_weight(who)-.07
            return .20 if self.pressure<=2 and own[SAFE] else .035 if self.pressure==0 and self.tokens[who]<10 else -1.
        if item==PIZZA:
            added=self.risk(who,strength,self.pressure+self.cfg.pizza_pressure)-p
            return self.hp_weight(who)*1.1-added*life-self.cfg.pizza_cost*cash
        if item==HOT:
            enemy_life=self.hp_weight(1-who)
            can_pass=BILL in self.inv[who] or CANDY in self.inv[who]
            after=self.pressure+strength*self.rules["step"]+(self.cfg.syrup if own[SYRUPED] else 0)
            next_risk=enemy_risk if can_pass else (1-p)*self.risk(1-who,opp_s,after)
            return (next_risk*enemy_life-(0 if can_pass or self.cfg.hot_opponent else p)*self.hp_weight(who))*.8-.035
        if item==STAMP:
            base=(1-p)*strength+p*5*(self.cfg.stamp_burst-1)
            if self.cfg.stamp_tip and own[TIPPED] and p>=self.cfg.tip_threshold: base+=(1-p)*self.cfg.tip
            return base*cash-.025
        if item==TIP:
            if p<self.cfg.tip_threshold:
                return -1.
            return (1-p)*self.cfg.tip*cash-.020
        return -1.

    def buy(self, who):
        if self.tokens[who]<self.rules["cost"] or self.rules["capacity"]-len(self.inv[who])<self.rules["pack"]: return False
        self.tokens[who]-=self.rules["cost"]
        drops=self.drop.sample(range(11),self.rules["pack"])
        self.inv[who].extend(drops); self.acquired.update(drops); self.buys[who]+=1
        return True

    def use(self, who, item):
        self.inv[who].remove(item); self.used[item]+=1
        own,other=self.fx[who],self.fx[1-who]
        if item==ICE: self.pressure=max(0,self.pressure-self.cfg.ice)
        elif item==CANDY: self.pressure+=self.cfg.candy
        elif item==SYRUP: other[SYRUPED]=True
        elif item==QUEUE: (own if self.cfg.queue_self else other)[SKIP]=True
        elif item==SPICY: other[FORCED]=True
        elif item==SAFETY: own[SAFE]=True
        elif item==PIZZA:
            self.hp[who]+=1; self.pressure+=self.cfg.pizza_pressure; self.tokens[who]-=self.cfg.pizza_cost
        elif item==HOT: self.hot=True; self.hot_target=1-who
        elif item==STAMP: own[DOUBLE]=True
        elif item==TIP: own[TIPPED]=True
        return item in (BILL,CANDY)

    def play(self, max_turns=500):
        while min(self.hp)>0 and self.turns<max_turns:
            who=self.actor; self.turns+=1
            if self.fx[who][SKIP]:
                self.fx[who][SKIP]=False; self.skips+=1; self.actor=1-who; continue
            strength=self.strength(who)
            used=0; ordered=False; passed=False
            while used<self.caps[who]:
                if self.policies[who]!="live": strength=self.strength(who)
                candidates=[(self.score(who,item,strength)*self.choice[who].uniform(.8,1.2),i,item)
                            for i,item in enumerate(self.inv[who])]
                choice=max(candidates,default=(-1,0,None))
                if choice[0]>.01:
                    passed=self.use(who,choice[2]); used+=1
                    if passed: self.passes+=1; break
                    continue
                if (not ordered or who==0 and self.caps[who]>8) and self.buy(who):
                    ordered=True; continue
                break
            if not passed:
                if self.fx[who][FORCED]: strength=3
                if self.policies[who]!="live": strength=self.strength(who)
                risk=self.risk(who,strength); burst=self.roll.random()<risk
                reward=self.reward(who,strength,risk,burst)
                plain=5 if burst else strength
                tip=self.cfg.tip if self.fx[who][TIPPED] and not burst and risk>=self.cfg.tip_threshold else 0
                if self.fx[who][DOUBLE]: self.stamp_profit+=reward-plain-tip
                if self.fx[who][TIPPED] and not burst and risk>=self.cfg.tip_threshold: self.tip_profit+=self.cfg.tip
                self.tokens[who]=min(9999,self.tokens[who]+reward); self.earned[who]+=reward
                self.actions+=1
                self.pressure+=strength*self.rules["step"]+(self.cfg.syrup if self.fx[who][SYRUPED] else 0)
                self.fx[who][:5]=[False]*5
                if burst:
                    self.hp[who]-=self.damage(who)
                    self.bursts+=1; self.new_bottle()
            self.actor=1-who
        return 0 if self.hp[0]>0 and self.hp[1]<=0 else 1 if self.hp[1]>0 and self.hp[0]<=0 else -1


PREVIOUS=LegacyProfile(ice=6,candy=2,syrup=4,safety=.25,queue_self=True,
    stamp_burst=1,stamp_tip=False,tip=4,pizza_cost=6,hot_opponent=True)


@dataclass(frozen=True)
class Profile:
    steps: tuple=(2,5,9)
    minimum: int=25
    maximum: int=35
    exponent: float=1.
    hp: int=4
    ice: int=6
    candy: int=6
    syrup: int=3
    spicy: int=2
    shield: int=1
    hot: int=1
    stamp: int=4
    tip: int=3
    order_cost: int=10
    order_pack: int=4
    bottle_items: int=0
    drop_weights: tuple=(1,)*11
    bottle_hot: bool=False


def runtime_profile():
    source=(ROOT/'Assets/_COLA_COLA/Scripts/Core/ColaItemBalance.cs').read_text(encoding='utf-8-sig')
    pressure=(ROOT/'Assets/_COLA_COLA/Scripts/Core/BottlePressureModel.cs').read_text(encoding='utf-8-sig')
    settings=(ROOT/'Assets/_COLA_COLA/Data/PressureInteractionSettings.asset').read_text(encoding='utf-8-sig')
    items=(ROOT/'Assets/_COLA_COLA/Scripts/Presentation/ColaItemController.cs').read_text(encoding='utf-8-sig')
    catalog=(ROOT/'Assets/_COLA_COLA/Scripts/Core/ColaItemCatalog.cs').read_text(encoding='utf-8-sig')
    def literal(text,name):
        return float(re.search(r'\b'+name+r'\s*=\s*([\d.]+)',text).group(1))
    return Profile(steps=tuple(int(literal(pressure,n)) for n in ('LowPressure','MediumPressure','HighPressure')),
        minimum=int(re.search(r'minimumBottleLimit: (\d+)',settings).group(1)),
        maximum=int(re.search(r'maximumBottleLimit: (\d+)',settings).group(1)),
        exponent=float(re.search(r'probabilityExponent: ([\d.]+)',settings).group(1)),
        order_cost=int(literal(items,'WaiterCost')),
        order_pack=int(literal(items,'WaiterItemCount')),
        drop_weights=tuple(int(w) for w in re.findall(r'new\(ColaItemId\.\w+, "[^"]+", \d+, (\d+),',catalog))+(0,),
        bottle_hot=True,tip=0,
        **{field:int(literal(source,name)) for field,name in {
            'hp':'StartingHealth','ice':'IceReduction','candy':'CandyIncrease','syrup':'SyrupBonus',
            'spicy':'SpicyMultiplier','shield':'ShieldReduction','hot':'HotDamageBonus',
            'stamp':'StampRewardBonus'}.items()})


class SimpleDuel:
    # Future-shake flags: shield, reward bonus, syrup, doubled pressure, extra damage.
    SHIELD,STAMPED,SYRUPED,SPICED,HEATED=range(5)
    def __init__(self,seed,cfg,policies=('balanced','balanced'),caps=(2,2),hp=None,tokens=(0,0),starter=None,hero=0):
        self.cfg=cfg; self.policies=policies; self.caps=caps
        self.drop=random.Random(seed^0x28D7); self.roll=random.Random(seed^0x915A)
        self.limit_rng=random.Random(seed^0x1911)
        self.choice=[random.Random(seed^0x719B),random.Random(seed^0x43A1)]
        self.hp=list(hp or (cfg.hp,cfg.hp)); self.maxhp=list(self.hp); self.tokens=list(tokens)
        self.inv=[self.draw_items(2),self.draw_items(2)]
        if starter is not None:
            self.inv[hero]=[starter,self.draw_items(1,exclude=starter)[0]]
        self.fx=[[False]*5,[False]*5]; self.pressure=0; self.limit=0; self.actor=0;self.hot_bottle=False
        self.turns=self.actions=self.bursts=self.passes=0
        self.used=Counter(); self.acquired=Counter(); self.strengths=Counter()
        for bag in self.inv:self.acquired.update(bag)
        self.buys=[0,0]; self.earned=[0,0]; self.remaining=2
        self.player_turns=0; self.boss_turns=0; self.boss_item_uses=0
        self.new_bottle()

    def draw_items(self,count,exclude=None):
        pool=[i for i,w in enumerate(self.cfg.drop_weights) if w>0 and i!=exclude]
        result=[]
        for _ in range(min(count,len(pool))):
            total=sum(self.cfg.drop_weights[i] for i in pool)
            ticket=self.drop.randrange(total)
            for i in pool:
                ticket-=self.cfg.drop_weights[i]
                if ticket<0:
                    result.append(i);pool.remove(i);break
        return result

    def new_bottle(self):
        replacement=self.limit>0
        value=self.limit_rng.randint(self.cfg.minimum,self.cfg.maximum)
        if value==self.limit and self.cfg.maximum>self.cfg.minimum:value=self.cfg.minimum+(value-self.cfg.minimum+1)%(self.cfg.maximum-self.cfg.minimum+1)
        self.limit=value; self.pressure=0;self.hot_bottle=False
        if replacement and min(self.hp)>0:
            for who in (0,1):
                drops=self.draw_items(min(self.cfg.bottle_items,8-len(self.inv[who])))
                self.inv[who].extend(drops);self.acquired.update(drops)

    def increase(self,who,strength):
        own=self.fx[who]
        return (self.cfg.steps[strength-1]+(self.cfg.syrup if own[self.SYRUPED] else 0))*(self.cfg.spicy if own[self.SPICED] else 1)

    def risk(self,who,strength,pressure=None):
        after=(self.pressure if pressure is None else pressure)+self.increase(who,strength)
        return 1. if after>=self.limit else (after/self.limit)**self.cfg.exponent

    def damage(self,who):
        own=self.fx[who]
        heated=self.hot_bottle if self.cfg.bottle_hot else own[self.HEATED]
        return max(0,1+(self.cfg.hot if heated else 0)-(self.cfg.shield if own[self.SHIELD] else 0))

    def hp_weight(self,who):
        return 2.4 if self.hp[who]<=1 else 1.4 if self.hp[who]==2 else 1.

    def coin_weight(self,who):
        return .12 if self.tokens[who]<self.cfg.order_cost else .065 if self.tokens[who]<self.cfg.order_cost*2 else .025

    def reward(self,who,strength,burst):
        return (5 if burst else strength)+(self.cfg.stamp if self.fx[who][self.STAMPED] else 0)

    def expected_opponent_strength(self,who):
        policy=self.policies[1-who]
        return 1 if policy=='cautious' else 3 if policy=='greedy' else 2

    def utility(self,who,strength,pressure=None,passing=False):
        pressure=self.pressure if pressure is None else pressure
        enemy=1-who; enemy_s=self.expected_opponent_strength(who)
        if passing:
            return self.risk(enemy,enemy_s,pressure)*self.damage(enemy)*self.hp_weight(enemy)*.55
        p=self.risk(who,strength,pressure)
        gain=(1-p)*self.reward(who,strength,False)+p*self.reward(who,strength,True)
        enemy_risk=self.risk(enemy,enemy_s,pressure+self.increase(who,strength))
        return self.coin_weight(who)*gain-p*self.damage(who)*self.hp_weight(who)+(1-p)*enemy_risk*self.damage(enemy)*self.hp_weight(enemy)*.55

    def strength(self,who):
        policy=self.policies[who]
        if policy=='cautious':return 1
        if policy=='greedy':return 3
        if policy in ('random','live'):return self.choice[who].randint(1,3)
        return max((self.utility(who,s)+self.choice[who].uniform(-.012,.012),s) for s in (1,2,3))[1]

    def usable(self,who,item):
        own,other=self.fx[who],self.fx[1-who]
        if item==ICE:return self.pressure>0
        if item==SYRUP:return not other[self.SYRUPED]
        if item==QUEUE:return bool(self.inv[1-who])
        if item==SPICY:return not other[self.SPICED]
        if item==SAFETY:return not own[self.SHIELD]
        if item==PIZZA:return self.hp[who]<self.maxhp[who]
        if item==HOT:return not (self.hot_bottle if self.cfg.bottle_hot else other[self.HEATED])
        if item==STAMP:return not own[self.STAMPED]
        if item==TIP:return self.tokens[who]<9999
        return True

    def score(self,who,item,strength):
        if not self.usable(who,item):return -1.
        own,other=self.fx[who],self.fx[1-who]
        before=self.utility(who,strength)
        if item==PIZZA:return self.hp_weight(who)*1.1
        if item==ICE:return self.utility(who,strength,max(0,self.pressure-self.cfg.ice))-before
        if item==CANDY:
            value=self.utility(who,strength,self.pressure+self.cfg.candy)-before
            if BILL in self.inv[who] and self.remaining>=2:
                value=max(value,self.utility(who,strength,self.pressure+self.cfg.candy,True)-before+.03)
            return value
        if item==BILL:return self.utility(who,strength,passing=True)-before
        if item==QUEUE:return .10+min(3,len(self.inv[1-who]))*.035
        if item==TIP:return self.coin_weight(who)*self.cfg.tip
        if item==HOT and self.cfg.bottle_hot:
            self.hot_bottle=True
            value=self.utility(who,strength)-before
            self.hot_bottle=False
            return value
        effects={SYRUP:(other,self.SYRUPED),SPICY:(other,self.SPICED),HOT:(other,self.HEATED),
                 SAFETY:(own,self.SHIELD),STAMP:(own,self.STAMPED)}
        state,index=effects[item]; state[index]=True
        value=self.utility(who,strength)-before
        state[index]=False
        return value

    def buy(self,who):
        if self.tokens[who]<self.cfg.order_cost or len(self.inv[who])>8-self.cfg.order_pack:return False
        self.tokens[who]-=self.cfg.order_cost; drops=self.draw_items(self.cfg.order_pack)
        self.inv[who].extend(drops); self.acquired.update(drops); self.buys[who]+=1
        return True

    def use(self,who,item):
        assert self.usable(who,item)
        self.inv[who].remove(item); self.used[item]+=1
        if who==1:self.boss_item_uses+=1
        own,other=self.fx[who],self.fx[1-who]
        if item==ICE:self.pressure=max(0,self.pressure-self.cfg.ice)
        elif item==CANDY:self.pressure+=self.cfg.candy
        elif item==SYRUP:other[self.SYRUPED]=True
        elif item==QUEUE:
            target=self.inv[1-who]; target.pop(self.drop.randrange(len(target)))
        elif item==SPICY:other[self.SPICED]=True
        elif item==SAFETY:own[self.SHIELD]=True
        elif item==PIZZA:self.hp[who]+=1
        elif item==HOT:
            if self.cfg.bottle_hot:self.hot_bottle=True
            else:other[self.HEATED]=True
        elif item==STAMP:own[self.STAMPED]=True
        elif item==TIP:
            self.tokens[who]=min(9999,self.tokens[who]+self.cfg.tip); self.earned[who]+=self.cfg.tip
        return item==BILL

    def play(self,max_turns=500):
        while min(self.hp)>0 and self.turns<max_turns:
            who=self.actor; self.turns+=1
            self.player_turns+=who==0; self.boss_turns+=who==1
            strength=self.strength(who); used=0; ordered=False; passed=False
            while used<self.caps[who]:
                self.remaining=self.caps[who]-used
                if self.policies[who]!='live':strength=self.strength(who)
                choices=[(self.score(who,item,strength)*self.choice[who].uniform(.8,1.2),i,item)
                         for i,item in enumerate(self.inv[who])]
                best=max(choices,default=(-1,0,None))
                if best[0]>.01:
                    passed=self.use(who,best[2]); used+=1
                    if passed:self.passes+=1; break
                elif (not ordered or who==0 and self.caps[who]>8) and self.buy(who):ordered=True
                else:break
            if not passed:
                if self.policies[who]!='live':strength=self.strength(who)
                p=self.risk(who,strength); burst=self.roll.random()<p
                reward=self.reward(who,strength,burst)
                self.tokens[who]=min(9999,self.tokens[who]+reward); self.earned[who]+=reward
                self.actions+=1; self.strengths[strength]+=1
                self.pressure+=self.increase(who,strength)
                if burst:
                    self.hp[who]-=self.damage(who); self.bursts+=1; self.new_bottle()
                self.fx[who]=[False]*5
            self.actor=1-who
        assert all(0<=t<=9999 for t in self.tokens) and all(len(bag)<=8 for bag in self.inv)
        assert all(h<=m for h,m in zip(self.hp,self.maxhp))
        return 0 if self.hp[1]<=0 else 1 if self.hp[0]<=0 else -1


def ci(wins,n):
    p=wins/n; z=1.96; den=1+z*z/n
    center=(p+z*z/(2*n))/den; d=z*math.sqrt(p*(1-p)/n+z*z/(4*n*n))/den
    return [round((center-d)*100,2),round((center+d)*100,2)]


def batch(cfg,n,policies=('balanced','balanced'),caps=(2,2),seed=10000,starter=None,hp=None,tokens=(0,0)):
    wins=draws=bursts=0; actions=[]; turns=[]; wallets=[]; packs=[]
    used=Counter(); acquired=Counter(); strengths=Counter()
    estimates={str(t):[] for t in (0,3,5,8)}
    cls=LegacyDuel if isinstance(cfg,LegacyProfile) else SimpleDuel
    for k in range(n):
        hero=k%2 if starter is not None else 0
        kwargs={'hp':hp or (3,3)} if cls==LegacyDuel else {'hp':hp}
        game=cls(seed+k,cfg,policies,caps,tokens=tokens,starter=starter,hero=hero,**kwargs)
        winner=game.play(); wins+=winner==hero; draws+=winner<0; bursts+=game.bursts
        actions.append(game.actions); turns.append(game.turns); wallets.append(max(game.tokens)); packs.append(sum(game.buys))
        used.update(game.used); acquired.update(game.acquired)
        if cls==SimpleDuel:
            strengths.update(game.strengths)
            base=game.actions*4.4+game.bursts*.99+game.boss_turns*.65+game.boss_item_uses*.65+game.buys[1]*.8
            for thinking in (0,3,5,8):estimates[str(thinking)].append((base+thinking*game.player_turns)/60)
    return {'n':n,'win_pct':round(wins/n*100,3),'ci95':ci(wins,n),'draws':draws,
        'actions_mean':round(statistics.mean(actions),2),'actions_p95':sorted(actions)[min(n-1,int(n*.95))],
        'turns_mean':round(statistics.mean(turns),2),'bursts_per_shake':round(bursts/max(1,sum(actions)),4),
        'actions_p10':sorted(actions)[min(n-1,int(n*.10))],'actions_median':statistics.median(actions),
        'estimated_minutes':{key:{'mean':round(statistics.mean(values),2),'p10':round(sorted(values)[int(n*.10)],2),
            'median':round(statistics.median(values),2),'p90':round(sorted(values)[min(n-1,int(n*.90))],2),
            'under3_pct':round(sum(t<3 for t in values)/n*100,2),'over10_pct':round(sum(t>10 for t in values)/n*100,2)}
            for key,values in estimates.items() if values},
        'packs_mean':round(statistics.mean(packs),2),'wallet_p95':sorted(wallets)[min(n-1,int(n*.95))],
        'items_used_mean':round(sum(used.values())/n,2),
        'strength_pct':{str(s):round(strengths[s]/max(1,sum(strengths.values()))*100,2) for s in (1,2,3)},
        'use_rates':{NAMES[i]:round(used[i]/max(1,acquired[i])*100,2) for i in range(11)}}


CANDIDATES={
    'old':PREVIOUS,
    'simple_hp3':Profile(hp=3,steps=(2,4,6)),
    'wide_hp3':Profile(hp=3),
    'wide_hp4':Profile(hp=4),
    'wide_hp5':Profile(hp=5),
    'linear_hp4':Profile(hp=4,steps=(3,6,9)),
    'double_hp4':Profile(hp=4,steps=(2,4,8)),
    'double_hp3':Profile(hp=3,steps=(2,4,8)),
    'double_hp3_ice4':Profile(hp=3,steps=(2,4,8),ice=4),
    'double_hp3_attack':Profile(hp=3,steps=(2,4,8),candy=8,syrup=4),
    'longer_hp4':Profile(hp=4,minimum=30,maximum=40),
    'curve_hp4':Profile(hp=4,exponent=1.25),
}


PACING_BEFORE=Profile(steps=(2,4,8),hp=4)
PACING_CANDIDATES={
    'current':PACING_BEFORE,
    'hp6_linear':replace(PACING_BEFORE,hp=6),
    'hp6_curve14':replace(PACING_BEFORE,hp=6,exponent=1.4),
    'hp6_curve16':replace(PACING_BEFORE,hp=6,exponent=1.6),
    'hp7_curve16':replace(PACING_BEFORE,hp=7,exponent=1.6),
    'hp6_curve18':replace(PACING_BEFORE,hp=6,exponent=1.8),
    'hp7_curve18':replace(PACING_BEFORE,hp=7,exponent=1.8),
    'hp6_curve20':replace(PACING_BEFORE,hp=6,exponent=2.),
    'hp6_curve16_economy':replace(PACING_BEFORE,hp=6,exponent=1.6,stamp=3,tip=2),
    'hp7_curve16_economy':replace(PACING_BEFORE,hp=7,exponent=1.6,stamp=3,tip=2),
    'hp6_curve16_cost12':replace(PACING_BEFORE,hp=6,exponent=1.6,order_cost=12),
    'hp6_curve18_cost12':replace(PACING_BEFORE,hp=6,exponent=1.8,order_cost=12),
    'hp7_curve16_cost12':replace(PACING_BEFORE,hp=7,exponent=1.6,order_cost=12),
    'hp6_curve16_limits35':replace(PACING_BEFORE,hp=6,exponent=1.6,minimum=30,maximum=40),
}


def pacing_screen(n,selected=None):
    output={'kind':'offline_pacing_model_not_unity','target_minutes':[5,8],'seed':11000000,
            'timing_assumptions':{'safe_shake':4.4,'extra_per_burst':.99,'boss_turn':.65,'boss_item':.65,'boss_order':.8,
                                  'player_thinking_seconds':[0,3,5,8]},'candidates':{}}
    for name,cfg in PACING_CANDIDATES.items():
        if selected and name not in selected:continue
        row={'profile':asdict(cfg)}
        for i,policy in enumerate(('balanced','greedy','cautious')):
            row[policy]=batch(cfg,n,(policy,'live'),(32,2),seed=11000000+i*20000)
        row['rich_stress']=batch(cfg,max(200,n//2),tokens=(50,50),seed=11100000)
        output['candidates'][name]=row
        print(name,'shakes',row['balanced']['actions_median'],'5s median',row['balanced']['estimated_minutes']['5']['median'],
              'p10',row['balanced']['estimated_minutes']['5']['p10'],'greedy',row['greedy']['estimated_minutes']['5']['median'],
              'rich p90',row['rich_stress']['estimated_minutes']['5']['p90'],flush=True)
    return output


def pacing_final(cfg,n,seed):
    output={'kind':'offline_pacing_model_not_unity','target_minutes':[5,8],'seed':seed,
            'previous_profile':asdict(PACING_BEFORE),'selected_profile':asdict(cfg),'comparison':{}}
    for label,tuning in [('before',PACING_BEFORE),('after',cfg)]:
        cases={}
        for i,policy in enumerate(('balanced','greedy','cautious','random')):
            cases[policy+'_vs_boss']=batch(tuning,n,(policy,'live'),(32,2),seed=seed+i*20000)
        cases['balanced_mirror']=batch(tuning,n,seed=seed+100000)
        cases['rich_stress']=batch(tuning,n//2,tokens=(50,50),seed=seed+120000)
        cases['stronger_boss']=batch(tuning,n//2,('balanced','live'),(32,2),hp=(tuning.hp,tuning.hp+3),seed=seed+140000)
        cases['starter_items']={NAMES[i]:batch(tuning,n//2,starter=i,seed=seed+180000) for i in range(11)}
        cases['strategies']={}
        for i,(left,right) in enumerate([('balanced','cautious'),('cautious','balanced'),('balanced','greedy'),('greedy','balanced'),('greedy','cautious'),('cautious','greedy')]):
            cases['strategies'][left+'_vs_'+right]=batch(tuning,n//2,(left,right),seed=seed+220000+i*10000)
        cases['campaign']=campaign(tuning,n//2,seed+400000)
        output['comparison'][label]=cases
        print(label,'balanced minutes',cases['balanced_vs_boss']['estimated_minutes']['5'],
              'shakes',cases['balanced_vs_boss']['actions_mean'],'rich',cases['rich_stress']['actions_p95'],flush=True)
    output['limitations']=['Offline statistical model only; no Unity Play, compiler, screenshots or listening.',
        'Time is estimated from existing authored clips/timings and assumed player decision time, not measured gameplay.',
        'Policies are bounded visible-state heuristics, not optimal play or human win rates.',
        'Purchases at decision boundaries; animations/FPS/input latency are not executed. No debug cheats.',
        '500-turn cutoff draws are reported. Late campaign stages may have very few samples.']
    return output


def screen(n,selected=None):
    output={'kind':'offline_virtual_simulation_not_unity','stage':'simplification_screen','seed':10000,'candidates':{}}
    for name,cfg in CANDIDATES.items():
        if selected and name not in selected:continue
        row={'profile':asdict(cfg),'mirror':batch(cfg,n),
             'greedy_vs_cautious':batch(cfg,n,('greedy','cautious')),
             'rich_stress':batch(cfg,max(300,n//2),tokens=(50,50))}
        row['starter_items']={NAMES[i]:batch(cfg,max(500,n//2),seed=150000,starter=i) for i in range(11)}
        rates=[test['win_pct'] for test in row['starter_items'].values()]
        row['starter_spread_pp']=round(max(rates)-min(rates),2)
        output['candidates'][name]=row
        print(name,'spread',row['starter_spread_pp'],'shakes',row['mirror']['actions_mean'],
              '3vs1',row['greedy_vs_cautious']['win_pct'],'choices',row['mirror']['strength_pct'],flush=True)
    return output


def campaign(cfg,n,seed):
    reached=[0]*8; beaten=[0]*8; cash=[[] for _ in range(8)]; matches=draws=0
    cls=LegacyDuel if isinstance(cfg,LegacyProfile) else SimpleDuel
    base=3 if cls==LegacyDuel else cfg.hp
    for k in range(n):
        game=cls(seed+k,cfg,('balanced','live'),(32,2))
        for level in range(8):
            reached[level]+=1; matches+=1
            winner=game.play(); draws+=winner<0
            if winner!=0:break
            beaten[level]+=1; cash[level].append(game.tokens[0])
            game.hp=[base,base+level+1]; game.maxhp=list(game.hp)
            game.tokens[1]=0; game.inv[1]=game.draw_items(2) if cls==SimpleDuel else game.drop.sample(range(11),2)
            game.fx=[[False]*(6 if cls==LegacyDuel else 5) for _ in range(2)]
            game.actor=0; game.actions=game.turns=0; game.new_bottle()
    return {'runs':n,'matches':matches,'draws':draws,'levels':[
        {'level':i+1,'reached':reached[i],'beaten':beaten[i],
         'conditional_win_pct':round(beaten[i]/max(1,reached[i])*100,2),
         'ci95':ci(beaten[i],reached[i]) if reached[i] else None,
         'carried_tokens_median':statistics.median(cash[i]) if cash[i] else None} for i in range(8)]}


def final(cfg,n,seed):
    result={'kind':'offline_virtual_simulation_not_unity','stage':'simplification_final','seed':seed,
            'selected_profile':asdict(cfg),'previous_profile':asdict(PREVIOUS),'comparison':{}}
    for label,tuning in [('before',PREVIOUS),('after',cfg)]:
        cases={}
        for policy in ('balanced','cautious','greedy','random'):
            cases[policy+'_mirror']=batch(tuning,n,(policy,policy),seed=seed)
        cases['greedy_vs_cautious']=batch(tuning,n,('greedy','cautious'),seed=seed+10000)
        cases['vs_live_boss']=batch(tuning,n,('balanced','live'),(32,2),seed=seed+20000)
        cases['rich_stress']=batch(tuning,n//2,tokens=(50,50),seed=seed+30000)
        hp=3 if isinstance(tuning,LegacyProfile) else tuning.hp
        cases['higher_hp_boss']=batch(tuning,n//2,('balanced','live'),(32,2),hp=(hp,hp+3),seed=seed+40000)
        for index,policy in enumerate(('balanced','cautious','greedy')):
            cases[policy+'_starter_items']={NAMES[i]:batch(tuning,n//2,(policy,policy),starter=i,seed=seed+50000+index*100000) for i in range(11)}
        cases['campaign']=campaign(tuning,n//2,seed+400000)
        result['comparison'][label]=cases
        print(label,{key:(val['win_pct'],val['actions_mean'],val['draws']) for key,val in cases.items() if 'n' in val},flush=True)
    result['limitations']=['Python rule model only; no Unity, compilation, Game view or listening.',
        'Bounded visible-state heuristic policies, not optimal play or human win rates.',
        'Next-shake effects expire on that actor committing a shake; all five combine and clear together.',
        'Mirror cases use 2 items/turn. Player-vs-boss cases use player 32, boss 2; purchases at decision boundaries.',
        'Different Python and C# RNG sequences; no future outcome access or debug cheats.',
        'Late campaign stages have few reached runs; infinite progression and all possible exploits are not proven.']
    return result


def model_contracts(cfg):
    checks=0
    def check(value):
        nonlocal checks
        assert value
        checks+=1
    game=SimpleDuel(72126,cfg)
    state=game.roll.getstate()
    for limit in range(cfg.minimum,cfg.maximum+1):
        game.limit=limit
        for pressure in range(limit+5):
            game.pressure=pressure
            values=[game.risk(0,s) for s in (1,2,3)]
            check(0<=values[0]<=values[1]<=values[2]<=1)
            for strength in (1,2,3):
                after=pressure+cfg.steps[strength-1]
                expected=1. if after>=limit else (after/limit)**cfg.exponent
                check(abs(game.risk(0,strength)-expected)<1e-12)
    check(game.roll.getstate()==state)
    game=SimpleDuel(72126,cfg); game.inv=[[SYRUP,SPICY,HOT],[SAFETY,STAMP]]
    for item in (SYRUP,SPICY,HOT):game.use(0,item)
    check(game.increase(1,3)==(cfg.steps[2]+cfg.syrup)*cfg.spicy)
    check(game.damage(1)==1+cfg.hot)
    game.use(1,SAFETY); check(game.damage(1)==max(0,1+cfg.hot-cfg.shield))
    game.use(1,STAMP); check(game.reward(1,3,True)==5+cfg.stamp)
    check(game.reward(1,1,False)==1+cfg.stamp)
    before=[row[:] for row in game.fx]; game.new_bottle(); check(game.fx==before)
    game=SimpleDuel(72126,cfg); game.inv=[[CANDY,QUEUE],[ICE,SYRUP]]
    state=game.roll.getstate(); check(not game.use(0,CANDY))
    check(game.actor==0 and game.pressure==cfg.candy and game.roll.getstate()==state)
    game.use(0,QUEUE); check(len(game.inv[1])==1 and len(game.inv[0])==0)
    game.inv[1]=[]; check(not game.usable(0,QUEUE))
    game.inv[0]=[PIZZA,TIP]; game.hp[0]=cfg.hp-1; game.tokens[0]=0
    game.use(0,PIZZA); check(game.hp[0]==cfg.hp and game.tokens[0]==0)
    check(not game.usable(0,PIZZA)); game.use(0,TIP)
    check(game.tokens[0]==cfg.tip and not any(game.fx[0]))
    game=SimpleDuel(72126,cfg); game.inv=[[],[]]
    game.fx=[[True]*5,[True]*5]; game.pressure=game.limit
    game.play(max_turns=1)
    check(not any(game.fx[0]) and all(game.fx[1]))
    check(game.tokens[0]==5+cfg.stamp)
    check(game.hp[0]==cfg.hp-max(0,1+cfg.hot-cfg.shield))
    game=SimpleDuel(72126,cfg); game.inv[0]=[ICE]*(9-cfg.order_pack); game.tokens[0]=cfg.order_cost
    state=game.drop.getstate(); check(not game.buy(0))
    check(game.tokens[0]==cfg.order_cost and game.drop.getstate()==state)
    owned=8-cfg.order_pack
    game.inv[0]=[ICE]*owned; check(game.buy(0))
    check(game.tokens[0]==0 and len(game.inv[0])==8 and len(set(game.inv[0][owned:]))==cfg.order_pack)
    state=game.drop.getstate(); game.inv[0]=[]
    check(not game.buy(0) and game.drop.getstate()==state)
    return {'kind':'offline_model_checks_not_unity','profile':asdict(cfg),'checks':checks,'passed':True}


def strategy_matrix(cfg,n,seed):
    results={}
    for left,right in [('balanced','cautious'),('cautious','balanced'),('balanced','greedy'),
                       ('greedy','balanced'),('greedy','cautious'),('cautious','greedy')]:
        row=batch(cfg,n,(left,right),seed=seed)
        results[left+'_vs_'+right]=row
        print(left,'vs',right,row['win_pct'],flush=True)
    return {'kind':'offline_strategy_comparison_not_unity','profile':asdict(cfg),'seed':seed,'cases':results}


def write_report():
    data=json.loads((DEST/'final.json').read_text(encoding='utf-8'))
    contracts=json.loads((DEST/'contracts.json').read_text(encoding='utf-8'))
    strategies=json.loads((DEST/'strategies.json').read_text(encoding='utf-8'))
    snapshot=json.loads(json.dumps(asdict(runtime_profile())))
    assert data['selected_profile']==contracts['profile']==strategies['profile']==snapshot and contracts['passed']
    before,after=data['comparison']['before'],data['comparison']['after']
    matches=draws=0
    for cases in (before,after):
        for key,test in cases.items():
            if key=='campaign':matches+=test['matches']; draws+=test['draws']
            elif 'starter_items' in key:
                for row in test.values():matches+=row['n']; draws+=row['draws']
            else:matches+=test['n']; draws+=test['draws']
    for row in strategies['cases'].values():matches+=row['n'];draws+=row['draws']
    n=after['balanced_mirror']['n']; starter_n=after['balanced_starter_items'][NAMES[0]]['n']
    cfg=runtime_profile(); timestamp=datetime.now(timezone.utc).isoformat()
    lines=['# 최신 결과: 단일 효과 아이템과 압력 가상 대전','',
        'Unity Play·컴파일·Game 뷰·소리 청취 검사를 실행하지 않았다. 아래 결과는 Python 규칙 모델에 한정된다.','',
        f'- 최종 비교 {matches:,}경기. 500차례 제한 무승부 {draws:,}회. 모델 조건 검사 {contracts["checks"]:,}개 통과.',
        f'- 선택 압력 +{cfg.steps[0]}/+{cfg.steps[1]}/+{cfg.steps[2]}, 공개 한계 {cfg.minimum}~{cfg.maximum}, 확률 = 흔든 뒤 압력 / 한계, 시작 체력 {cfg.hp}.',
        '- 시작 무작위 2개, 8칸, 벨 10토큰/무작위 4개, 기본 안전 보상 1/2/3·폭발 5는 유지한다.',
        '- 모든 아이템의 추가 사용료·조건부 팁·보상 배수 예외·차례 건너뛰기를 제거했다.','',
        '## 현재 효과','',
        '| 아이템 | 단일 효과 |','|---|---|',
        f'| 얼음컵 | 압력 -{cfg.ice} |',f'| 발포 캔디 | 압력 +{cfg.candy} |',
        f'| 탄산 시럽 | 상대 다음 흔들기 압력 +{cfg.syrup} |',
        '| 취소 번호표 | 상대 아이템 1개 무작위 제거 |','| 계산서 | 흔들지 않고 상대에게 차례 넘김 |',
        f'| 매운맛 주문서 | 상대 다음 흔들기 압력 {cfg.spicy}배 |',
        f'| 안전 마개 | 내 다음 흔들기 피해 -{cfg.shield} |','| 피자 한 조각 | 체력 +1 |',
        f'| 핫소스 | 상대 다음 흔들기 피해 +{cfg.hot} |',
        f'| 더블 스탬프 | 내 다음 흔들기 보상 +{cfg.stamp}토큰 |',f'| 팁 봉투 | 즉시 +{cfg.tip}토큰 |','',
        '공통 규칙: 다음 흔들기 효과는 그 사람이 흔들면 한 번에 소모한다. 안전하게 끝나도 소모하며 병 교체·계산서로 소모되지 않는다. 같은 효과는 중첩되지 않는다. 아이템은 계산서 외에는 자기 차례를 유지한다.','',
        '## 시작품에 따른 차이','',
        f'관심 아이템 1개 + 무작위 1개, 선공·후공 절반씩. 아이템·전략·버전당 {starter_n:,}경기, 양쪽 최대 2개 사용/턴.','',
        '| 전략 | 이전 최저~최고 | 새 최저~최고 | 새 범위 (%p) |','|---|---:|---:|---:|']
    for policy,label in [('balanced','기대 보상·체력 판단'),('cautious','강도 1'),('greedy','강도 3')]:
        a=[v['win_pct'] for v in before[policy+'_starter_items'].values()]
        b=[v['win_pct'] for v in after[policy+'_starter_items'].values()]
        lines.append(f'| {label} | {min(a):.2f}~{max(a):.2f}% | {min(b):.2f}~{max(b):.2f}% | {max(b)-min(b):.2f} |')
    lines+=['','판단형 전략의 상세 수치. Wilson 95% 구간이며 여러 항목의 동시 비교를 보정한 구간은 아니다.','',
        '| 아이템 | 새 승률 | 95% 구간 | 사용/획득 비율 |','|---|---:|---:|---:|']
    for name in NAMES:
        row=after['balanced_starter_items'][name]; rates=after['balanced_mirror']['use_rates']
        lines.append(f'| {name} | {row["win_pct"]:.2f}% | {row["ci95"][0]:.2f}~{row["ci95"][1]:.2f}% | {rates[name]:.2f}% |')
    lines+=['','## 강도 선택과 경기 길이','',f'각 기본 사례 {n:,}경기. 승률은 표의 첫 전략/플레이어 기준이며 앞의 시작품 비교와 다르다.','',
        '| 사례 | 새 승률 | 95% 구간 | 평균 흔들기 | 95백분위 |','|---|---:|---:|---:|---:|']
    for key,label in [('balanced_mirror','판단형 대 판단형 / 선공'),('cautious_mirror','강도 1 대 1 / 선공'),
        ('greedy_mirror','강도 3 대 3 / 선공'),('random_mirror','무작위 대 무작위 / 선공'),
        ('greedy_vs_cautious','강도 3 대 강도 1'),('vs_live_boss','현재 보스 AI / 플레이어'),
        ('rich_stress','양쪽 50토큰 / 선공'),('higher_hp_boss',f'체력 {cfg.hp} 대 {cfg.hp+3} / 플레이어')]:
        row=after[key]
        lines.append(f'| {label} | {row["win_pct"]:.2f}% | {row["ci95"][0]:.2f}~{row["ci95"][1]:.2f}% | {row["actions_mean"]:.2f} | {row["actions_p95"]} |')
    choices=after['balanced_mirror']['strength_pct']
    lines+=['',f'판단형 전략의 실제 강도 선택: 1번 {choices["1"]:.2f}%, 2번 {choices["2"]:.2f}%, 3번 {choices["3"]:.2f}%.',
        '개별 전략의 승률은 그 전략의 능력을 보증하지 않는다. 미래 판정을 읽지 않는 제한된 휴리스틱만 비교했다.','',
        '다른 전략을 서로 대전시킨 추가 비교. 두 자리를 모두 바꿔 선공의 영향을 분리한다.','',
        '| 좌측 전략 대 우측 전략 | 좌측 승률 | 95% 구간 |','|---|---:|---:|']
    for label,row in strategies['cases'].items():
        lines.append(f'| {label} | {row["win_pct"]:.2f}% | {row["ci95"][0]:.2f}~{row["ci95"][1]:.2f}% |')
    lines+=['',
        '## 연속 보스전','',f'조정 후 {after["campaign"]["runs"]:,}런. 플레이어 아이템·잔액 유지, 체력 완충, 보스 체력 단계당 +1. 뒤 단계는 도달 수가 적어 결론 근거로 삼지 않는다.','',
        '| 단계 | 도달 | 승리 | 조건부 승률 | 잔액 중앙값 |','|---|---:|---:|---:|---:|']
    for row in after['campaign']['levels']:
        rate=f'{row["conditional_win_pct"]:.2f}%' if row['reached'] else '—'
        cash=row['carried_tokens_median'] if row['carried_tokens_median'] is not None else '—'
        lines.append(f'| {row["level"]} | {row["reached"]:,} | {row["beaten"]:,} | {rate} | {cash} |')
    lines+=['','## 범위와 한계','',
        '- 이전/새 규칙에 맞춘 아이템 판단을 각각 사용한다. 이 비교는 효과 변경만의 인과 추정이나 최적 전략 증명이 아니다.',
        '- 미러 비교는 2개/턴, 실제 보스 모형은 플레이어 32개/턴·보스 2개/턴이다. 구매는 판단 시점에만 수행한다.',
        '- 압력 판정·아이템 추첨·한계·판단 난수는 분리한다. 미래 판정 접근과 Q/W/E는 사용하지 않는다. Python과 C# 난수 시퀀스는 다르다.',
        '- 화면·클릭·애니메이션·사람의 심리·무한 단계·모든 악용 조합은 검사하지 않았다.','',
        '[현재 효과](../../../Docs/Gameplay/Items.md) · [설계와 재실행](../../../Docs/Gameplay/ItemBalance.md) · [최종 데이터](item-balance/final.json) · [후보 비교](item-balance/screen.json) · [전략 비교](item-balance/strategies.json) · [모델 검사](item-balance/contracts.json)','']
    latest=DEST.parent
    (latest/'Summary.md').write_text('\n'.join(lines),encoding='utf-8')
    status={'kind':'offline_virtual_simulation_not_unity','suite':'simple-items-pressure-balance','phase':'completed',
        'completedUtc':timestamp,'success':True,'playMode':False,'unityExecution':False,'compilationRun':False,
        'simulatedMatches':matches,'turnLimitDraws':draws,'modelChecks':contracts['checks'],'profile':snapshot}
    (latest/'status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (latest/'verification.log').write_text(f'{timestamp} OFFLINE_SIMPLE_ITEMS_COMPLETED\nMatches={matches}; draws={draws}; model checks={contracts["checks"]}\nUnityPlay=NOT_RUN; Compile=NOT_RUN; GameView=NOT_RUN; Listening=NOT_RUN\nFinal seed bank={data["seed"]}; seconds={data["elapsed_seconds"]}\n',encoding='utf-8')
    print('report',latest/'Summary.md','matches',matches,'draws',draws,flush=True)


def pacing_report():
    directory=ROOT/'output/verification/latest/pacing-balance'
    data=json.loads((directory/'final.json').read_text(encoding='utf-8'))
    contracts=json.loads((directory/'contracts.json').read_text(encoding='utf-8'))
    snapshot=json.loads(json.dumps(asdict(runtime_profile())))
    assert data['selected_profile']==contracts['profile']==snapshot and contracts['passed']
    before,after=data['comparison']['before'],data['comparison']['after']
    matches=draws=0
    for cases in (before,after):
        for key,test in cases.items():
            if key=='campaign':matches+=test['matches'];draws+=test['draws']
            elif key in ('starter_items','strategies'):
                for row in test.values():matches+=row['n'];draws+=row['draws']
            else:matches+=test['n'];draws+=test['draws']
    cfg=runtime_profile(); timestamp=datetime.now(timezone.utc).isoformat()
    a,b=before['balanced_vs_boss'],after['balanced_vs_boss']
    lines=['# 최신 결과: 한 보스당 5~8분을 위한 밸런스','',
        '**가상 대전과 시간 추정 결과다. Unity Play·컴파일·Game 뷰·청취 검사는 실행하지 않았다.**','',
        f'- 최종 비교 {matches:,}경기, 500차례 제한 무승부 {draws:,}회. 모델 조건 검사 {contracts["checks"]:,}개 통과.',
        '- 목표는 첫 보스와의 일반적인 한 경기 5~8분이다. Q/W/E를 사용하지 않고 일반 확률로 계산했다.',
        '- 모든 아이템의 단일 효과, 시작품 2개, 8칸, 기본 토큰 1/2/3·폭발 5, 강도 압력 +2/+4/+8, 공개 한계 25~35는 유지한다.','',
        '## 적용한 변경','',
        '| 항목 | 이전 | 현재 |','|---|---|---|',
        f'| 플레이어·첫 보스 체력 | 4 | {cfg.hp} |',
        f'| 폭발 확률 | 압력비 | 압력비의 {cfg.exponent}제곱 |',
        f'| 벨: 무작위 4개 | 10토큰 | {cfg.order_cost}토큰 |','',
        '초반 위험을 줄이고 한계 근처에서 위험이 커지는 곡선으로 바꿨다. 체력은 행동 기회를 늘리고, 벨 가격은 늘어난 토큰 수입과 회복·방어 아이템의 반복 공급을 조절한다. 보스의 토큰 판단 기준과 벨 설명도 같은 가격을 사용한다. 연출 길이는 바꾸지 않았다.','',
        '## 경기 길이','',
        '플레이어가 자기 차례마다 평균 5초 동안 버튼·아이템을 판단한다고 가정한 시간이다. 실제 플레이를 재거나 기다린 시간이 아니다.','',
        '| 전략 대 현재 보스 AI | 이전 중앙값 | 현재 중앙값 | 현재 10~90백분위 | 현재 평균 흔들기 |','|---|---:|---:|---:|---:|']
    for policy,label in [('balanced','상황에 맞춰 선택'),('greedy','강도 3 위주'),('cautious','강도 1 위주'),('random','무작위 강도')]:
        old=before[policy+'_vs_boss']['estimated_minutes']['5']
        new=after[policy+'_vs_boss']['estimated_minutes']['5']
        lines.append(f'| {label} | {old["median"]:.2f}분 | {new["median"]:.2f}분 | {new["p10"]:.2f}~{new["p90"]:.2f}분 | {after[policy+"_vs_boss"]["actions_mean"]:.2f}회 |')
    lines+=['',f'- 판단형의 3분 미만 종료 비율: {a["estimated_minutes"]["5"]["under3_pct"]:.2f}% → {b["estimated_minutes"]["5"]["under3_pct"]:.2f}%.',
        f'- 판단형의 10분 초과 비율: {b["estimated_minutes"]["5"]["over10_pct"]:.2f}%. 모든 경기가 5~8분에 들어간다는 뜻은 아니다.','',
        '| 자기 차례의 평균 판단 시간 | 현재 경기 중앙값 |','|---|---:|']
    for seconds in (0,3,5,8):lines.append(f'| {seconds}초 | {b["estimated_minutes"][str(seconds)]["median"]:.2f}분 |')
    lines+=['',
        '시간 모델: 실제 에셋의 흔들기 클립 4.4초, 코드의 폭발/복귀 추가 약 0.99초, 보스 생각 0.65초, 보스 아이템당 0.65초, 보스 주문당 0.8초를 더한다. 플레이어 판단 시간은 그 차례의 아이템 선택까지 포함한다. 프레임 지연·마우스 이동·메뉴를 오래 읽는 시간은 실행하거나 측정하지 않았다.','',
        '## 확률과 선택지','',
        '| 압력비 | 이전 확률 | 현재 확률 |','|---|---:|---:|']
    for fraction in (.2,.5,.8,1.):lines.append(f'| {fraction*100:.0f}% | {fraction*100:.1f}% | {fraction**cfg.exponent*100:.1f}% |')
    lines+=['',f'한계 30의 새 콜라에서 강도 1/2/3은 약 '+
        '/'.join(f'{(step/30)**cfg.exponent*100:.1f}%' for step in cfg.steps)+'다. 한계 도달은 100%다.','',
        '| 전략 조합 | 앞 전략 승률 | 95% 구간 |','|---|---:|---:|']
    labels={'balanced':'판단형','cautious':'강도 1','greedy':'강도 3'}
    for name,row in after['strategies'].items():
        left,right=name.split('_vs_')
        lines.append(f'| {labels[left]} 대 {labels[right]} | {row["win_pct"]:.2f}% | {row["ci95"][0]:.2f}~{row["ci95"][1]:.2f}% |')
    choices=after['balanced_mirror']['strength_pct']
    lines+=['',f'양쪽 판단형의 강도 선택 비율: 1번 {choices["1"]:.2f}%, 2번 {choices["2"]:.2f}%, 3번 {choices["3"]:.2f}%.',
        '이 수치는 지정한 휴리스틱끼리의 결과이며 사람의 승률이나 최적 전략을 증명하지 않는다.','',
        '## 아이템과 경제','',
        '| 시작 아이템 1개 지정 + 무작위 1개 | 보유자 승률 | 95% 구간 |','|---|---:|---:|']
    for name,row in after['starter_items'].items():lines.append(f'| {name} | {row["win_pct"]:.2f}% | {row["ci95"][0]:.2f}~{row["ci95"][1]:.2f}% |')
    rich=after['rich_stress']
    lines+=['',f'시작품 비교는 아이템당 {next(iter(after["starter_items"].values()))["n"]:,}경기, 선공/후공 절반씩, 양쪽 최대 2개/턴이다. 실제 보스 모형은 플레이어 최대 32개/턴, 보스 2개/턴이다.',
        f'양쪽 50토큰 시작 스트레스: 평균 {rich["actions_mean"]:.2f}회, 95백분위 {rich["actions_p95"]}회 흔들기, 5초 판단 기준 90백분위 {rich["estimated_minutes"]["5"]["p90"]:.2f}분. 큰 자산으로 시작하는 상황은 일반 경기보다 길어질 수 있다.','',
        '## 연속 보스전','',
        '플레이어 체력 완충, 미사용 아이템·잔액 유지, 보스 체력 +1/단계는 그대로다. 후반은 도달 표본이 적을 수 있다.','',
        '| 단계 | 도달 | 승리 | 조건부 승률 | 잔액 중앙값 |','|---|---:|---:|---:|---:|']
    for row in after['campaign']['levels']:
        rate=f'{row["conditional_win_pct"]:.2f}%' if row['reached'] else '—'
        cash=row['carried_tokens_median'] if row['carried_tokens_median'] is not None else '—'
        lines.append(f'| {row["level"]} | {row["reached"]:,} | {row["beaten"]:,} | {rate} | {cash} |')
    lines+=['','## 범위','',
        '현재 수치를 읽는 Python 규칙 모델이다. 난수 스트림은 분리하며 미래 판정은 보지 않는다. 후보 선별과 최종 비교의 시드는 다르다. Unity와 Python의 난수열은 같지 않다. UI·프레임·실제 체감·모든 조합·무한 난이도는 확인하지 않았다.','',
        '[최종 데이터](pacing-balance/final.json) · [후보 비교](pacing-balance/screen.json) · [모델 검사](pacing-balance/contracts.json) · [현재 규칙](../../../Docs/Gameplay/Items.md) · [설계](../../../Docs/Gameplay/PacingBalance.md)','']
    latest=directory.parent
    (latest/'Summary.md').write_text('\n'.join(lines),encoding='utf-8')
    status={'kind':'offline_pacing_model_not_unity','suite':'pacing-balance','phase':'completed','success':True,
        'completedUtc':timestamp,'simulatedMatches':matches,'turnLimitDraws':draws,'modelChecks':contracts['checks'],
        'playMode':False,'compilationRun':False,'unityExecution':False,'profile':snapshot,
        'thinkingSecondsPerPlayerTurn':5,'estimatedMedianMinutes':b['estimated_minutes']['5']['median']}
    (latest/'status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    (latest/'verification.log').write_text(f'{timestamp} OFFLINE_PACING_COMPLETED\nMatches={matches}; draws={draws}; model checks={contracts["checks"]}\nUnityPlay=NOT_RUN; Compile=NOT_RUN; ActualTimeMeasurement=NOT_RUN\nEstimated time uses authored timing plus assumed player thinking.\nSeed={data["seed"]}; seconds={data["elapsed_seconds"]}\n',encoding='utf-8')
    print('report',latest/'Summary.md','matches',matches,flush=True)


def supply_screen(cfg,n,seed):
    base=replace(cfg,order_cost=12,order_pack=4,bottle_items=0)
    candidates={'current':base,'bottle1':replace(base,bottle_items=1),
        'bell6for2':replace(base,order_cost=6,order_pack=2),
        'bell5for2':replace(base,order_cost=5,order_pack=2),
        'both':replace(base,bottle_items=1,order_cost=6,order_pack=2)}
    output={'kind':'offline_supply_comparison_not_unity','seed':seed,'candidates':{}}
    for name,tuning in candidates.items():
        row={'profile':asdict(tuning),'balanced':batch(tuning,n,('balanced','live'),(32,2),seed=seed),
            'greedy':batch(tuning,n//2,('greedy','live'),(32,2),seed=seed+100000),
            'rich':batch(tuning,n//2,tokens=(50,50),seed=seed+200000)}
        output['candidates'][name]=row
        print(name,row['balanced']['estimated_minutes']['5'],flush=True)
    return output


def supply_final(cfg,n,seed):
    previous=replace(cfg,order_cost=12,order_pack=4,bottle_items=0)
    output={'kind':'offline_supply_model_not_unity','seed':seed,'selected_profile':asdict(cfg),'comparison':{}}
    for label,tuning in [('before',previous),('after',cfg)]:
        cases={'profile':asdict(tuning)}
        for i,policy in enumerate(('balanced','greedy','cautious')):
            cases[policy]=batch(tuning,n,(policy,'live'),(32,2),seed=seed+i*10000)
        cases['mirror']=batch(tuning,n,seed=seed+40000)
        cases['rich']=batch(tuning,n//2,tokens=(50,50),seed=seed+50000)
        cases['campaign']=campaign(tuning,n//4,seed+60000)
        output['comparison'][label]=cases
        print(label,cases['balanced']['estimated_minutes']['5'],'uses',cases['balanced']['items_used_mean'],flush=True)
    output['contracts']=model_contracts(cfg)
    return output


def supply_report():
    directory=ROOT/'output/verification/latest/item-supply'
    data=json.loads((directory/'final.json').read_text(encoding='utf-8'))
    screen=json.loads((directory/'screen.json').read_text(encoding='utf-8'))
    snapshot=json.loads(json.dumps(asdict(runtime_profile())))
    assert data['selected_profile']==data['contracts']['profile']==snapshot and data['contracts']['passed']
    matches=draws=0
    for cases in data['comparison'].values():
        for key,test in cases.items():
            if key=='profile':continue
            matches+=test['matches'] if key=='campaign' else test['n']
            draws+=test['draws']
    screened=sum(row[key]['n'] for row in screen['candidates'].values() for key in ('balanced','greedy','rich'))
    before=data['comparison']['before'];after=data['comparison']['after']
    lines=['# 최신 결과: 웨이터벨 6토큰 / 랜덤 2개','',
        '**가상 대전 결과다. Unity Play·컴파일·실제 Game 뷰·청취 검사는 실행하지 않았다.**','',
        '- 2번 방식을 적용했다. 플레이어와 보스 모두 벨 12토큰/4개 → **6토큰/2개**다.',
        '- 빈 칸 2개가 필요하며 묶음 안에서는 중복이 없다. 콜라당 제한 없이 경기 중 주문한다.',
        '- 시작 무작위 2개, 체력 7, 압력 +2/+4/+8, 한계 25~35, 지수 1.6과 아이템 효과를 유지한다.',
        '- 콜라 교체 때 무료 추가 지급은 도입하지 않았다. 초기 시작품은 두 후보 모두 기존 2개다.','',
        '## 후보 비교','',
        '각 후보 판단형 대 현재 보스 모형 1,600경기 + 강도 3 위주 800경기 + 양쪽 50토큰 스트레스 800경기다. 아래 시간은 자기 차례 평균 5초 판단을 가정한 추정치다.','',
        '| 후보 | 일반 중앙값 | 10분 초과 | 경기당 아이템 사용 합계 | 50토큰 스트레스 90백분위 |',
        '|---|---:|---:|---:|---:|']
    names={'current':'이전 12토큰/4개','bottle1':'콜라 교체마다 양쪽 1개 무료 + 이전 벨',
        'bell6for2':'6토큰/2개 벨 (적용)','bell5for2':'5토큰/2개 벨','both':'콜라 교체마다 양쪽 1개 무료 + 6토큰/2개 벨'}
    for name,row in screen['candidates'].items():
        t=row['balanced']['estimated_minutes']['5']
        lines.append(f'| {names[name]} | {t["median"]:.2f}분 | {t["over10_pct"]:.2f}% | {row["balanced"]["items_used_mean"]:.2f}개 | {row["rich"]["estimated_minutes"]["5"]["p90"]:.2f}분 |')
    lines+=['','무료 지급은 매번 교체되는 병에 연동해 회복·방어·보상 아이템을 더 공급하며 긴 경기 비율을 늘렸다. 6토큰/2개는 개당 평균 가격 3토큰을 유지하면서 초기 주문까지 필요한 토큰과 빈 칸을 절반으로 줄인다. 5토큰/2개보다 과잉 공급이 적었다.','',
        '## 별도 난수로 최종 비교','',
        '| 전략 | 이전 중앙값 | 현재 중앙값 | 현재 10~90백분위 |',
        '|---|---:|---:|---:|']
    for key,title in [('balanced','판단형'),('greedy','강도 3 위주'),('cautious','강도 1 위주')]:
        old=before[key]['estimated_minutes']['5'];new=after[key]['estimated_minutes']['5']
        lines.append(f'| {title} | {old["median"]:.2f}분 | {new["median"]:.2f}분 | {new["p10"]:.2f}~{new["p90"]:.2f}분 |')
    rich=after['rich'];normal=after['balanced']['estimated_minutes']['5']
    lines+=['',f'후보 {screened:,}경기와 별도 최종 {matches:,}경기. 최종 500차례 제한 무승부 {draws}회. 모델 조건 검사 {data["contracts"]["checks"]:,}개 통과.',
        f'현재 판단형의 10분 초과는 {normal["over10_pct"]:.2f}%, 자산 50토큰 스트레스의 시간 90백분위는 {rich["estimated_minutes"]["5"]["p90"]:.2f}분이다. 긴 경기 가능성은 남아 있다.',
        f'판단형 미러의 선공 승률 {after["mirror"]["win_pct"]:.2f}% (95% 구간 {after["mirror"]["ci95"]}). 사람의 승률이나 최적 전략을 의미하지 않는다.','',
        '## 범위와 재현','',
        'Python 규칙 모델의 비교이며 압력·아이템·한계·판단 난수는 분리했다. 실제 보스 모형은 최대 2개/턴, 플레이어는 32개/턴으로 모델링한다. 구매는 판단 경계에서 수행하며 보스와 대전하는 사람의 실시간 벨 사용을 그대로 재현하지 않는다. Q/W/E는 사용하지 않았다. 시간은 기존 애니메이션·연출 길이에 내 차례당 5초 판단을 더한 추정이며 직접 측정하지 않았다. 후반 8단계 캠페인 결과는 원본 데이터에 있고 뒤 단계 표본은 적다.',
        '', '[최종 데이터](item-supply/final.json) · [후보 비교](item-supply/screen.json) · [설계와 재실행](../../../Docs/Gameplay/ItemSupplyBalance.md) · [현재 규칙](../../../Docs/Gameplay/Items.md)','']
    latest=directory.parent;timestamp=datetime.now(timezone.utc).isoformat()
    (latest/'Summary.md').write_text('\n'.join(lines),encoding='utf-8')
    status={'kind':'offline_supply_model_not_unity','phase':'completed','success':True,'completedUtc':timestamp,
        'screenedMatches':screened,'simulatedMatches':matches,'turnLimitDraws':draws,'modelChecks':data['contracts']['checks'],
        'playMode':False,'compilationRun':False,'unityExecution':False,'profile':snapshot,
        'estimatedMedianMinutes':normal['median'],'thinkingSecondsPerPlayerTurn':5}
    (latest/'status.json').write_text(json.dumps(status,ensure_ascii=False,indent=2),encoding='utf-8')
    (latest/'verification.log').write_text(f'{timestamp} OFFLINE_ITEM_SUPPLY_COMPLETED\nScreened={screened}; final={matches}; draws={draws}; model checks={data["contracts"]["checks"]}\nUnityPlay=NOT_RUN; Compile=NOT_RUN; GameView=NOT_RUN; Listening=NOT_RUN\nTime is an estimate, not measured gameplay.\n',encoding='utf-8')
    print('report',latest/'Summary.md','matches',matches,flush=True)


def weighted_contracts(cfg):
    checks=0
    def check(value):
        nonlocal checks
        assert value
        checks+=1
    check(cfg.bottle_hot and len(cfg.drop_weights)==11 and cfg.drop_weights[TIP]==0)
    check(sum(cfg.drop_weights)==100 and all(w>0 for w in cfg.drop_weights[:TIP]))
    game=SimpleDuel(2468,cfg);state=game.roll.getstate()
    for limit in range(cfg.minimum,cfg.maximum+1):
        game.limit=limit
        for pressure in range(limit+3):
            game.pressure=pressure
            p=[game.risk(0,s) for s in (1,2,3)]
            check(0<=p[0]<=p[1]<=p[2]<=1)
    check(game.roll.getstate()==state)
    game=SimpleDuel(2468,cfg);game.inv=[[HOT,HOT,BILL],[SAFETY]]
    game.use(0,HOT);check(game.hot_bottle and game.damage(0)==2 and game.damage(1)==2)
    check(not game.usable(0,HOT))
    game.use(1,SAFETY);check(game.damage(1)==1 and game.damage(0)==2)
    game.fx[1]=[False]*5;check(game.hot_bottle and game.damage(1)==2)
    game.use(0,BILL);check(game.hot_bottle)
    game.new_bottle();check(not game.hot_bottle and game.damage(0)==1 and game.damage(1)==1)
    game=SimpleDuel(2468,cfg);game.inv=[[HOT],[STAMP]];game.use(0,HOT)
    game.limit=999;game.pressure=0;game.inv[0]=[]
    game.play(max_turns=1);check(game.hot_bottle and game.damage(0)==2)
    game.limit=1;game.pressure=0;game.actor=1;game.inv=[[],[]]
    old=game.hp[1];game.play(max_turns=2)
    check(game.hp[1]==old-2 and not game.hot_bottle)
    game=SimpleDuel(2468,cfg);game.inv[0]=[ICE]*7;game.tokens[0]=cfg.order_cost
    state=game.drop.getstate();check(not game.buy(0) and game.drop.getstate()==state and game.tokens[0]==cfg.order_cost)
    game.inv[0]=[ICE]*6;check(game.buy(0) and len(game.inv[0])==8 and game.tokens[0]==0)
    check(len(set(game.inv[0][-2:]))==2 and TIP not in game.inv[0])
    state=game.drop.getstate();game.inv[0]=[]
    check(not game.buy(0) and game.drop.getstate()==state)
    counts=Counter();trials=20000;state=game.roll.getstate()
    for _ in range(trials):
        drops=game.draw_items(2)
        check(len(drops)==2 and len(set(drops))==2 and TIP not in drops)
        counts.update(drops)
    check(game.roll.getstate()==state)
    total=sum(cfg.drop_weights)
    rows=[]
    for i,w in enumerate(cfg.drop_weights[:TIP]):
        inclusion=w/total+sum(v/total*w/(total-v) for j,v in enumerate(cfg.drop_weights) if j!=i and v>0)
        rows.append({'name':NAMES[i],'weight':w,'first_draw_pct':round(w/total*100,3),
            'pack_inclusion_pct':round(inclusion*100,3),'observed_pack_pct':round(counts[i]/trials*100,3)})
    check(abs(sum(row['pack_inclusion_pct'] for row in rows)-200)<.01)
    return {'kind':'python_rule_checks_not_unity','checks':checks,'passed':True,'profile':asdict(cfg),'pack_probabilities':rows}


def weighted_final(cfg,n,seed):
    previous=Profile(steps=(2,4,8),hp=7,exponent=1.6,order_cost=6,order_pack=2)
    output={'kind':'offline_weighted_items_not_unity','seed':seed,'selected_profile':asdict(cfg),'comparison':{}}
    output['contracts']=weighted_contracts(cfg)
    for label,tuning in [('before',previous),('after',cfg)]:
        cases={'profile':asdict(tuning)}
        for i,policy in enumerate(('balanced','greedy','cautious')):
            if label=='before' and policy!='balanced':continue
            cases[policy]=batch(tuning,n,(policy,'live'),(32,2),seed=seed+i*10000)
        cases['mirror']=batch(tuning,n,seed=seed+40000)
        cases['rich']=batch(tuning,n//2,tokens=(50,50),seed=seed+50000)
        cases['campaign']=campaign(tuning,n//4,seed+60000)
        if label=='after':
            cases['starters']={NAMES[i]:batch(tuning,n//3,starter=i,seed=seed+100000) for i in range(TIP)}
            cases['strategies']={}
            for i,(left,right) in enumerate([('balanced','cautious'),('cautious','balanced'),('balanced','greedy'),('greedy','balanced'),('greedy','cautious'),('cautious','greedy')]):
                cases['strategies'][left+'_vs_'+right]=batch(tuning,n//3,(left,right),seed=seed+150000+i*10000)
        output['comparison'][label]=cases
        print(label,cases['balanced']['estimated_minutes']['5'],'rich90',cases['rich']['estimated_minutes']['5']['p90'],flush=True)
    return output


def weighted_screen(cfg,n,seed):
    before=Profile(steps=(2,4,8),hp=7,exponent=1.6,order_cost=6,order_pack=2)
    a=(8,17,15,12,8,14,5,3,12,6,0);b=(8,15,14,12,8,13,7,5,10,8,0)
    base=replace(before,bottle_hot=True,tip=0)
    candidates={'previous':before,'uniform10':replace(base,drop_weights=(1,)*10+(0,)),
        **{'rare'+str(h):replace(base,hp=h,drop_weights=a) for h in (7,8,9)},
        **{'moderate'+str(h):replace(base,hp=h,drop_weights=b) for h in (7,8)},
        'rare10':replace(base,hp=10,drop_weights=a),'moderate9':replace(base,hp=9,drop_weights=b)}
    output={'kind':'offline_weighted_items_screen_not_unity','seed':seed,'candidates':{}}
    for name,tuning in candidates.items():
        row={'profile':asdict(tuning),'balanced':batch(tuning,n,('balanced','live'),(32,2),seed=seed),
            'greedy':batch(tuning,n//2,('greedy','live'),(32,2),seed=seed+100000),
            'rich':batch(tuning,n//2,tokens=(50,50),seed=seed+200000)}
        output['candidates'][name]=row
        print(name,row['balanced']['estimated_minutes']['5'],flush=True)
    return output


def weighted_report():
    directory=ROOT/'output/verification/latest/weighted-items'
    data=json.loads((directory/'final.json').read_text(encoding='utf-8'))
    screen=json.loads((directory/'screen.json').read_text(encoding='utf-8'))
    snapshot=json.loads(json.dumps(asdict(runtime_profile())))
    assert data['selected_profile']==data['contracts']['profile']==snapshot and data['contracts']['passed']
    matches=draws=0
    for cases in data['comparison'].values():
        for key,test in cases.items():
            if key=='profile':continue
            if key in ('starters','strategies'):
                matches+=sum(row['n'] for row in test.values());draws+=sum(row['draws'] for row in test.values())
            else:
                matches+=test['matches'] if key=='campaign' else test['n'];draws+=test['draws']
    screened=sum(row[key]['n'] for row in screen['candidates'].values() for key in ('balanced','greedy','rich'))
    before=data['comparison']['before'];after=data['comparison']['after']
    old=before['balanced']['estimated_minutes']['5'];new=after['balanced']['estimated_minutes']['5']
    lines=['# 최신 결과: 콜라 전체 핫소스와 가중 아이템 지급','',
        '**Python 가상 매치·모델 조건 결과다. Unity Play·컴파일·Game 뷰·청취 검사는 실행하지 않았다.**','',
        '- 피해 +1 아이템은 핫소스 하나다. 현재 콜라가 교체될 때까지 양쪽 폭발 피해가 2가 된다. 마개가 있으면 1이며 중첩되지 않는다.',
        '- 안전한 흔들기·차례 넘김·상대 행동으로 핫소스가 소모되지 않는다. 병 교체·새 경기·전체 초기화 때 해제한다.',
        '- 팁 봉투를 카탈로그·지급·E 메뉴·사용 효과에서 제거하고 모델·메타데이터·제작 코드를 삭제했다. 현재 10종이다.',
        '- 시작품과 벨에 같은 가중치를 쓴다. 두 품목은 중복 없이 뽑는다. 보스도 동일하다.',
        '- 기본 체력 7 → 10. 벨 6토큰/2개, 8칸, 무작위 시작 2개, 압력 +2/+4/+8, 한계 25~35, 지수 1.6은 유지한다.','',
        '## 지급 확률','',
        '첫 추첨의 합은 100%다. 두 번째는 첫 품목을 제외해 재정규화한다. 묶음 포함 확률은 2개 중 한 번이라도 들어갈 확률이므로 합은 200%다.','',
        '| 품목 | 첫 추첨 | 2개 묶음 포함 |', '|---|---:|---:|']
    for row in data['contracts']['pack_probabilities']:
        lines.append(f'| {row["name"]} | {row["first_draw_pct"]:.0f}% | {row["pack_inclusion_pct"]:.2f}% |')
    lines+=['','피자·안전 마개·얼음컵·계산서의 비중을 낮추고, 토큰 수입을 늘려 재구매를 촉진하는 스탬프도 낮췄다. 각 묶음의 두 번째 품목은 기존 보유품과 겹칠 수 있다. E는 확률을 적용하지 않고 현재 품목을 모두 보여준다.','',
        '## 경기 길이와 경제','',
        f'후보 선별 {screened:,}경기, 별도 난수 최종 {matches:,}경기. 최종 500차례 제한 무승부 {draws}회. Python 모델 조건 {data["contracts"]["checks"]:,}개 통과.',
        f'판단형 대 현재 보스 모형: 중앙값 {old["median"]:.2f} → {new["median"]:.2f}분. 현재 10~90백분위 {new["p10"]:.2f}~{new["p90"]:.2f}분, 3분 미만 {new["under3_pct"]:.2f}%, 10분 초과 {old["over10_pct"]:.2f}% → {new["over10_pct"]:.2f}%.',
        f'양쪽 50토큰 스트레스의 90백분위 {before["rich"]["estimated_minutes"]["5"]["p90"]:.2f} → {after["rich"]["estimated_minutes"]["5"]["p90"]:.2f}분.','',
        '시간은 기존 흔들기 4.4초·폭발 추가 약 0.99초·보스 대기/아이템/주문 지연에 내 차례당 평균 5초 판단을 더한 추정이다. 실제 Play 시간을 재지 않았다. 모든 경기·사람·난이도가 5~8분 안에 든다는 의미는 아니다.','',
        '| 전략 대 보스 | 현재 중앙값 | 10~90백분위 |','|---|---:|---:|']
    for key,title in [('balanced','판단형'),('greedy','강도 3 위주'),('cautious','강도 1 위주')]:
        t=after[key]['estimated_minutes']['5'];lines.append(f'| {title} | {t["median"]:.2f}분 | {t["p10"]:.2f}~{t["p90"]:.2f}분 |')
    lines+=['','## 시작품과 전략','',
        '시작품 1개를 지정하고 나머지 1개는 해당 품목을 제외한 가중 추첨으로 지급했다. 품목당 1,000경기, 선공/후공 절반씩, 양쪽 최대 2개 사용/턴이다.','',
        '| 지정 품목 | 보유자 승률 | 95% 구간 |','|---|---:|---:|']
    for name,row in after['starters'].items():lines.append(f'| {name} | {row["win_pct"]:.2f}% | {row["ci95"][0]:.2f}~{row["ci95"][1]:.2f}% |')
    lines+=['','피자와 스탬프는 지정 지급 시 이점이 남아 있어 각각 첫 추첨 3%와 6%로 제한했다. 모든 아이템을 같은 성능으로 만들었다는 뜻은 아니다.','',
        '| 전략 조합 | 앞 전략 승률 | 95% 구간 |','|---|---:|---:|']
    for name,row in after['strategies'].items():lines.append(f'| {name} | {row["win_pct"]:.2f}% | {row["ci95"][0]:.2f}~{row["ci95"][1]:.2f}% |')
    lines += ['',f'판단형 미러 강도 비율: {after["mirror"]["strength_pct"]}. 미러 선공 승률 {after["mirror"]["win_pct"]:.2f}%. 보수적 강도 1 전략은 강도 3 위주보다 강했고 경기 길이도 길었다. 판단형은 최적 전략으로 증명한 정책이 아니다.',
        '', '## 범위와 자료','',
        '미래 난수를 읽지 않는 공개 상태 휴리스틱이다. 실제 보스 모형은 아이템 최대 2개/턴, 플레이어는 32개/턴으로 모델링하고 주문은 판단 경계에서 수행한다. Q/W/E·프레임·클릭·애니메이션·소리를 실행하지 않았다. C#과 Python의 난수 시퀀스는 다르다. 750런씩 최대 8단계 캠페인을 포함하지만 후반 도달 표본이 작아 무한 난이도를 보증하지 않는다. 가중 추첨은 모델에서 20,000묶음의 중복·삭제 품목 배제·난수 분리도 확인했다.',
        '', '[최종 데이터](weighted-items/final.json) · [후보 비교](weighted-items/screen.json) · [현재 효과](../../../Docs/Gameplay/Items.md) · [설계와 재실행](../../../Docs/Gameplay/WeightedItemBalance.md)','']
    latest=directory.parent;timestamp=datetime.now(timezone.utc).isoformat()
    (latest/'Summary.md').write_text('\n'.join(lines),encoding='utf-8')
    (latest/'status.json').write_text(json.dumps({'kind':'offline_weighted_items_not_unity','phase':'completed','success':True,
        'completedUtc':timestamp,'screenedMatches':screened,'simulatedMatches':matches,'turnLimitDraws':draws,
        'modelChecks':data['contracts']['checks'],'playMode':False,'compilationRun':False,'unityExecution':False,
        'profile':snapshot,'estimatedMedianMinutes':new['median'],'thinkingSecondsPerPlayerTurn':5},ensure_ascii=False,indent=2),encoding='utf-8')
    (latest/'verification.log').write_text(f'{timestamp} OFFLINE_WEIGHTED_ITEMS_COMPLETED\nScreened={screened}; Final={matches}; Draws={draws}; ModelChecks={data["contracts"]["checks"]}\nUnityPlay=NOT_RUN; Compile=NOT_RUN; GameView=NOT_RUN; Listening=NOT_RUN\nTime is estimated, not measured gameplay.\n',encoding='utf-8')
    print('report',latest/'Summary.md','matches',matches,flush=True)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--phase',choices=['screen','final','contracts','strategies','report','pacing-screen','pacing-final','pacing-contracts','pacing-report','supply-screen','supply-final','supply-report','weighted-screen','weighted-final','weighted-report'],default='screen')
    parser.add_argument('--profile',default='wide_hp4')
    parser.add_argument('--games',type=int,default=1000)
    parser.add_argument('--seed',type=int,default=5000000)
    parser.add_argument('--profiles',help='Comma-separated screen candidates')
    args=parser.parse_args(); start=time.time()
    if args.phase=='report':write_report();return
    if args.phase=='pacing-report':pacing_report();return
    if args.phase=='supply-report':supply_report();return
    if args.phase=='weighted-report':weighted_report();return
    cfg=runtime_profile() if args.profile=='runtime' else PACING_CANDIDATES[args.profile] if args.phase.startswith('pacing-') else CANDIDATES[args.profile]
    if args.phase=='weighted-screen':data=weighted_screen(cfg,args.games,args.seed)
    elif args.phase=='weighted-final':data=weighted_final(cfg,args.games,args.seed)
    elif args.phase=='supply-screen':data=supply_screen(cfg,args.games,args.seed)
    elif args.phase=='supply-final':data=supply_final(cfg,args.games,args.seed)
    elif args.phase=='pacing-screen':data=pacing_screen(args.games,args.profiles.split(',') if args.profiles else None)
    elif args.phase=='pacing-final':data=pacing_final(cfg,args.games,args.seed)
    elif args.phase=='pacing-contracts':data=model_contracts(cfg)
    elif args.phase=='contracts':data=model_contracts(cfg)
    elif args.phase=='strategies':data=strategy_matrix(cfg,args.games,args.seed)
    elif args.phase=='screen':data=screen(args.games,args.profiles.split(',') if args.profiles else None)
    else:data=final(cfg,args.games,args.seed)
    data['elapsed_seconds']=round(time.time()-start,2)
    directory=ROOT/'output/verification/latest/weighted-items' if args.phase.startswith('weighted-') else ROOT/'output/verification/latest/item-supply' if args.phase.startswith('supply-') else ROOT/'output/verification/latest/pacing-balance' if args.phase.startswith('pacing-') else DEST
    directory.mkdir(parents=True,exist_ok=True)
    path=directory/(args.phase.replace('pacing-','').replace('supply-','').replace('weighted-','')+'.json'); path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
    print('saved',path,'seconds',data['elapsed_seconds'],flush=True)


if __name__=='__main__':main()

