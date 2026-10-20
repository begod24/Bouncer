#!/usr/bin/env python3
"""
Bouncer: UI-спрайты в стиле «мел на асфальте» и дудл-иконки для карточек-вкладышей.

Запуск из корня репозитория:  python3 Tools/ui_art.py
Нужны numpy и Pillow. HUD белый — цвет задаётся в Unity (Image.color); иконки вкладышей тёмные.
Иконки мячей (волейбольный, набивной, теннисный, сдутый) рендерит Unity из 3D-моделей, их здесь нет.
Сиды фиксированы: повторный запуск даёт те же картинки.
"""
import math
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "UI")
INK = (43, 54, 70)


def blur(a, radius):
    """Гауссово размытие float-массива (отдельно по осям)."""
    if radius <= 0:
        return a
    r = max(1, int(math.ceil(radius * 3)))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / radius) ** 2)
    k /= k.sum()
    a = np.apply_along_axis(lambda v: np.convolve(v, k, mode="same"), 1, a)
    return np.apply_along_axis(lambda v: np.convolve(v, k, mode="same"), 0, a)


class Canvas:
    def __init__(self, w, h, seed):
        self.w, self.h = w, h
        self.rng = np.random.default_rng(seed)
        ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
        self.x, self.y = xs + 0.5, ys + 0.5
        self.alpha = np.zeros((h, w), np.float32)
        self.edge = self.noise(5.0) - 0.5

    def noise(self, cell_x, cell_y=None):
        cell_y = cell_y or cell_x
        gw, gh = int(self.w / cell_x) + 2, int(self.h / cell_y) + 2
        grid = (self.rng.random((gh, gw)) * 255).astype(np.uint8)
        img = Image.fromarray(grid).resize((self.w, self.h), Image.BICUBIC)
        return np.asarray(img, np.float32) / 255.0

    def stroke(self, pts, width, closed=False, opacity=1.0, rough=1.2, dash=0):
        pts = [tuple(map(float, p)) for p in pts]
        if closed:
            pts.append(pts[0])
        dist = np.full((self.h, self.w), 1e9, np.float32)
        travelled = 0.0
        for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
            dx, dy = x1 - x0, y1 - y0
            length = math.hypot(dx, dy)
            if dash and int(travelled // dash) % 2 == 1:
                travelled += length
                continue
            travelled += length
            if length < 1e-6:
                t = np.zeros_like(self.x)
            else:
                t = np.clip(((self.x - x0) * dx + (self.y - y0) * dy) / (length * length), 0.0, 1.0)
            dist = np.minimum(dist, np.hypot(self.x - (x0 + t * dx), self.y - (y0 + t * dy)))
        half = width / 2.0 + self.edge * rough
        self.alpha = np.maximum(self.alpha, np.clip(half - dist + 0.5, 0.0, 1.0) * opacity)

    def fill(self, pts, opacity=1.0, hatch=None, soft=0.8):
        """Заливка многоугольника; hatch=(угол, шаг) — штриховка, как мелом."""
        inside = np.zeros((self.h, self.w), bool)
        n = len(pts)
        for i in range(n):
            x0, y0 = pts[i]
            x1, y1 = pts[(i + 1) % n]
            if y0 == y1:
                continue
            crosses = (y0 > self.y) != (y1 > self.y)
            x_at = x0 + (self.y - y0) * (x1 - x0) / (y1 - y0)
            inside ^= crosses & (self.x < x_at)
        cover = blur(inside.astype(np.float32), soft)
        if hatch:
            angle, step = hatch
            a = math.radians(angle)
            phase = (self.x * math.cos(a) + self.y * math.sin(a)) / step * 2 * math.pi
            wobble = (self.noise(12.0) - 0.5) * 2.0
            cover = cover * np.clip((np.sin(phase + wobble) + 0.35) * 1.6, 0.0, 1.0)
        self.alpha = np.maximum(self.alpha, cover * opacity)

    @staticmethod
    def circle_pts(cx, cy, r, a0=0.0, a1=360.0, n=72):
        return [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a)))
                for a in np.linspace(a0, a1, n)]

    @staticmethod
    def heart_pts(cx, cy, s, n=90):
        pts = []
        for t in np.linspace(0, 2 * math.pi, n, endpoint=False):
            x = 16 * math.sin(t) ** 3
            y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
            pts.append((cx + x * s, cy - y * s))
        return pts

    @staticmethod
    def round_rect_pts(x0, y0, x1, y1, r, n=8):
        pts = []
        for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
            for a in np.linspace(a0, a0 + 90, n):
                pts.append((cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))))
        return pts

    def arrow_head(self, tip, angle_deg, size, width):
        a = math.radians(angle_deg)
        for side in (-1, 1):
            b = a + math.pi + side * math.radians(28)
            self.stroke([tip, (tip[0] + size * math.cos(b), tip[1] + size * math.sin(b))], width)

    def save(self, name, chalky=True, grain=0.45, pits=0.07, color=(255, 255, 255)):
        a = self.alpha
        if chalky:
            fine = blur(self.rng.random((self.h, self.w)).astype(np.float32), 0.6)
            fine = (fine - fine.min()) / max(1e-6, fine.max() - fine.min())
            streak = self.noise(9.0, 2.0)
            texture = 0.65 * fine + 0.35 * streak
            a = a * np.clip(1.0 - grain + grain * texture * 1.4, 0.0, 1.0)
            a = np.where(self.rng.random((self.h, self.w)) < pits, a * 0.35, a)
        rgba = np.zeros((self.h, self.w, 4), np.uint8)
        rgba[..., :3] = color
        rgba[..., 3] = np.clip(a * 255.0 + 0.5, 0, 255).astype(np.uint8)
        os.makedirs(os.path.dirname(os.path.join(OUT, name)), exist_ok=True)
        Image.fromarray(rgba).save(os.path.join(OUT, name))
        print("wrote", name)


def wobble(pts, amount, seed):
    rng = np.random.default_rng(seed)
    return [(x + rng.uniform(-amount, amount), y + rng.uniform(-amount, amount)) for x, y in pts]


def dense(pts, step=3.0):
    """Ломаная, разбитая на короткие отрезки: пунктир (dash) режет только по отрезкам."""
    out = []
    for (x0, y0), (x1, y1) in zip(pts[:-1], pts[1:]):
        n = max(1, int(math.hypot(x1 - x0, y1 - y0) / step))
        out += [(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t) for t in np.linspace(0, 1, n, endpoint=False)]
    out.append(pts[-1])
    return out


def rotate(pts, cx, cy, degrees, dx=0.0, dy=0.0):
    """Поворот вокруг (cx, cy) (в координатах картинки: минус — против часовой) и сдвиг."""
    a = math.radians(degrees)
    ca, sa = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * ca - (y - cy) * sa + dx, cy + (x - cx) * sa + (y - cy) * ca + dy) for x, y in pts]


def hud():
    c = Canvas(128, 128, 1)
    heart = c.heart_pts(64, 60, 3.2)
    c.fill(heart, opacity=0.55, hatch=(35, 7))
    c.stroke(heart, 7, closed=True)
    c.save("HUD_Heart.png")

    c = Canvas(128, 128, 2)
    c.stroke(c.heart_pts(64, 60, 3.2), 5, closed=True, opacity=0.8, dash=9)
    c.save("HUD_HeartEmpty.png")

    c = Canvas(128, 128, 3)
    ball = c.circle_pts(64, 64, 46)
    c.fill(ball, opacity=0.45, hatch=(-30, 7))
    c.stroke(ball, 7, closed=True)
    c.stroke(c.circle_pts(64 + 62, 64 - 58, 88, 118, 166, 24), 6)
    c.save("HUD_Ball.png")

    c = Canvas(128, 128, 4)
    c.stroke(c.circle_pts(64, 64, 46), 5, closed=True, opacity=0.8, dash=10)
    c.save("HUD_BallEmpty.png")

    c = Canvas(256, 64, 5)
    c.stroke(wobble(c.round_rect_pts(8, 9, 248, 55, 10), 1.2, 5), 6, closed=True)
    c.save("HUD_BarFrame.png")

    c = Canvas(256, 64, 6)
    c.fill(c.round_rect_pts(12, 13, 244, 51, 7), opacity=1.0, hatch=(60, 6))
    c.save("HUD_BarFill.png", grain=0.3)

    c = Canvas(128, 128, 7)
    c.stroke(c.circle_pts(64, 64, 34), 7, closed=True)
    for a in (45, 135, 225, 315):
        r0, r1 = 46, 60
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(64 + ca * r1, 64 + sa * r1), (64 + ca * r0, 64 + sa * r0)], 7)
        c.arrow_head((64 + ca * r0, 64 + sa * r0), a + 180, 12, 6)
    c.save("HUD_Catch.png")

    c = Canvas(128, 128, 8)
    c.stroke([(34, 64), (104, 64)], 9)
    c.arrow_head((106, 64), 0, 26, 9)
    for y, x0 in ((40, 18), (88, 18)):
        c.stroke([(x0, y), (x0 + 34, y)], 6, opacity=0.8)
    c.stroke([(8, 64), (22, 64)], 6, opacity=0.8)
    c.save("HUD_Dash.png")

    c = Canvas(512, 40, 9)
    pts = [(12 + i * 4.9, 22 + 5 * math.sin(i * 0.33) + 3 * math.sin(i * 1.7)) for i in range(100)]
    c.stroke(pts, 7)
    c.stroke([(x, y + 8) for x, y in pts[10:70]], 4, opacity=0.6)
    c.save("HUD_Underline.png")

    c = Canvas(128, 128, 10)
    coin = c.circle_pts(64, 64, 46)
    c.fill(coin, opacity=0.35, hatch=(30, 7))
    c.stroke(coin, 7, closed=True)
    c.stroke(c.circle_pts(64, 64, 35), 4, closed=True, opacity=0.6)
    for y in (46, 58):
        c.stroke([(46, y), (82, y)], 7)
    c.stroke([(64, 58), (64, 90)], 7)
    c.save("HUD_Coin.png")

    c = Canvas(128, 128, 11)
    lid = c.circle_pts(64, 64, 46)
    c.fill(lid, opacity=0.3, hatch=(30, 7))
    c.stroke(lid, 7, closed=True)
    c.stroke(c.circle_pts(64, 64, 33), 4, closed=True, opacity=0.6)
    knob = c.circle_pts(64, 64, 11)
    c.fill(knob, opacity=0.9)
    c.stroke(knob, 5, closed=True)
    c.save("HUD_Lid.png")

    c = Canvas(256, 128, 12)
    c.stroke([(18, 64), (196, 64)], 18)
    c.arrow_head((238, 64), 0, 62, 18)
    for y in (40, 88):
        c.stroke([(26, y), (86, y)], 7, opacity=0.7)
    c.save("Exit_Arrow.png")

    c = Canvas(256, 320, 13)
    c.stroke(wobble([(28, 70), (228, 70), (228, 306), (28, 306)], 1.5, 13), 7, closed=True)
    c.stroke([(18, 70), (238, 70)], 7)
    for x, top in ((64, 34), (186, 42)):
        c.stroke([(x, 70), (x, top)], 4)
        c.stroke([(x - 14, top + 8), (x + 14, top + 8)], 4)
        c.stroke([(x - 9, top + 18), (x + 9, top + 18)], 4)
    c.stroke([(112, 306), (112, 266), (144, 266), (144, 306)], 6)
    c.stroke([(102, 260), (154, 260)], 5)
    c.save("HUD_Panelka.png")

    c = Canvas(64, 64, 14)
    window = c.round_rect_pts(10, 8, 54, 56, 4)
    c.fill(window, opacity=0.85, hatch=(45, 6))
    c.stroke(window, 5, closed=True)
    c.stroke([(32, 10), (32, 54)], 3, opacity=0.8)
    c.save("HUD_WindowLit.png")

    c = Canvas(64, 64, 15)
    c.stroke(c.round_rect_pts(10, 8, 54, 56, 4), 4, closed=True, opacity=0.85)
    c.save("HUD_WindowDark.png")


def card_frames():
    c = Canvas(360, 520, 20)
    tooth, depth, top, bottom = 18.0, 9.0, 6.0, 514.0
    pts = []
    x = 6.0
    while x < 354.0:
        pts += [(x, top + depth), (min(354.0, x + tooth / 2), top)]
        x += tooth
    pts += [(354.0, top + depth), (354.0, bottom - depth)]
    x = 354.0
    while x > 6.0:
        pts += [(x, bottom - depth), (max(6.0, x - tooth / 2), bottom)]
        x -= tooth
    pts += [(6.0, bottom - depth)]
    c.fill(pts, soft=0.7)
    c.save("Card_Paper.png", chalky=False)

    c = Canvas(320, 320, 21)
    c.fill(c.round_rect_pts(4, 4, 316, 316, 22), soft=0.7)
    c.save("Card_Window.png", chalky=False)

    c = Canvas(400, 560, 22)
    c.stroke(c.round_rect_pts(22, 22, 378, 538, 26), 10, closed=True, rough=0)
    c.alpha = np.clip(blur(c.alpha, 6.0) * 2.2, 0, 1)
    c.save("Card_Glow.png", chalky=False)

    c = Canvas(360, 520, 23)
    c.stroke(pts, 12, closed=True, rough=0)
    c.save("Card_RarityFrame.png", chalky=False)


def icons():
    ink = 11

    c = Canvas(256, 256, 30)
    arc = c.circle_pts(128, 136, 78, 205, 505, 90)
    c.stroke(arc, ink)
    end = arc[-1]
    c.arrow_head(end, math.degrees(math.atan2(end[1] - arc[-4][1], end[0] - arc[-4][0])), 30, ink)
    ball = c.circle_pts(56, 104, 26)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.save("Icons/Icon_Boomerang.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 31)
    ball = c.circle_pts(72, 128, 34)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    for tip in ((222, 58), (222, 198)):
        c.stroke([(118, 128), (150, 128), tip], ink)
        c.arrow_head(tip, math.degrees(math.atan2(tip[1] - 128, tip[0] - 150)), 28, ink)
    c.save("Icons/Icon_Split.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 32)
    ball = c.circle_pts(128, 184, 40)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    spring = [(128 + (18 if i % 2 else -18) * (0 < i < 12), 142 - i * 9.5) for i in range(13)]
    c.stroke(spring, 7)
    c.stroke(c.circle_pts(128, 24, 14), 8, closed=True)
    c.save("Icons/Icon_Elastic.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 33)
    for cx, cy in ((96, 152), (148, 132)):
        ball = c.circle_pts(cx, cy, 44)
        c.fill(ball, opacity=0.45, hatch=(30, 8))
        c.stroke(ball, 8, closed=True)
    c.stroke([(206, 40), (206, 96)], 12)
    c.stroke([(178, 68), (234, 68)], 12)
    c.save("Icons/Icon_ExtraBall.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 34)
    shoe = [(70, 190), (70, 138), (104, 128), (130, 96), (162, 96), (170, 134), (228, 152), (236, 190)]
    c.fill(shoe, opacity=0.35, hatch=(-30, 9))
    c.stroke(shoe, 9, closed=True)
    c.stroke([(64, 202), (240, 202)], 12)
    for i in range(3):
        c.stroke([(136 + i * 12, 110 + i * 8), (152 + i * 12, 116 + i * 8)], 5)
    for y, x0 in ((120, 14), (150, 4), (180, 14)):
        c.stroke([(x0, y), (x0 + 42, y)], 7, opacity=0.85)
    c.save("Icons/Icon_Speed.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 35)
    ball = c.circle_pts(128, 132, 38)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.stroke(c.circle_pts(128, 132, 78, 120, 240, 30), ink)
    c.stroke(c.circle_pts(128, 132, 78, -60, 60, 30), ink)
    for a in (-90, -60, -120):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(128 + ca * 92, 132 + sa * 92), (128 + ca * 112, 132 + sa * 112)], 7)
    c.save("Icons/Icon_Catch.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 36)
    c.stroke(c.circle_pts(84, 128, 52, 90, 270, 40), 26, rough=0.6)
    c.stroke([(84, 76), (132, 76)], 26, rough=0.6)
    c.stroke([(84, 180), (132, 180)], 26, rough=0.6)
    for y in (76, 180):
        c.stroke([(118, y - 13), (118, y + 13)], 5, opacity=0.9)
    ball = c.circle_pts(206, 128, 30)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    for x in (146, 160):
        c.stroke(c.circle_pts(x - 60, 128, 60, -22, 22, 10), 5, opacity=0.8)
    c.save("Icons/Icon_Pickup.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 37)
    bread = c.round_rect_pts(40, 120, 216, 196, 26)
    c.fill(bread, opacity=0.35, hatch=(40, 10))
    c.stroke(bread, 9, closed=True)
    sausage = [(64 + 128 * t, 108 - 26 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 20)]
    sausage += [(192 - 128 * t, 126 - 12 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 20)]
    c.fill(sausage, opacity=0.55, hatch=(-30, 8))
    c.stroke(sausage, 8, closed=True)
    for x in (96, 128, 160):
        c.stroke(c.circle_pts(x, 104, 6), 4, closed=True, opacity=0.8)
    c.save("Icons/Icon_Sandwich.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 38)
    c.stroke([(12, 226), (244, 226)], 9)
    fall = [(14 + 90 * t, 222 - 104 * (1 - t * t)) for t in np.linspace(0, 1, 24)]
    hop = [(104 + 96 * t, 222 - 120 * (2 * t - t * t)) for t in np.linspace(0, 0.8, 20)]
    c.stroke(fall + hop[1:], 7, dash=13)
    for tip in ((82, 192), (126, 192), (104, 186)):
        c.stroke([(104 + (tip[0] - 104) * 0.45, 214 + (tip[1] - 214) * 0.45), tip], 6)
    ball = c.circle_pts(204, 84, 30)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.save("Icons/Icon_Bouncy.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 39)
    potato = wobble([(128 + 84 * math.cos(a) + 6 * math.cos(3 * a), 160 + 56 * math.sin(a))
                     for a in np.linspace(0, 2 * math.pi, 60, endpoint=False)], 1.5, 39)
    c.fill(potato, opacity=0.4, hatch=(30, 9))
    c.stroke(potato, 9, closed=True)
    for x, y in ((96, 146), (150, 176), (172, 140)):
        c.stroke(c.circle_pts(x, y, 6, 200, 340, 8), 5)
    for x in (88, 128, 168):
        c.stroke([(x + 9 * math.sin(i * 1.1), 92 - i * 11) for i in range(7)], 7)
    for a in (192, 168, -12, 12):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(128 + ca * 100, 160 + sa * 70), (128 + ca * 122, 160 + sa * 84)], 7)
    c.save("Icons/Icon_HotPotato.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 40)
    splat = wobble([(96 + 72 * math.cos(a) * (1 + 0.12 * math.sin(5 * a)), 206 + 26 * math.sin(a) * (1 + 0.12 * math.sin(5 * a)))
                    for a in np.linspace(0, 2 * math.pi, 60, endpoint=False)], 1.5, 40)
    c.fill(splat, opacity=0.45, hatch=(-30, 8))
    c.stroke(splat, 8, closed=True)
    ball = c.circle_pts(186, 66, 34)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    for (x0, y0), (x1, y1), sag in (((66, 196), (164, 92), 22), ((102, 190), (180, 100), -12), ((136, 196), (196, 98), 14)):
        length = math.hypot(x1 - x0, y1 - y0)
        nx, ny = (y1 - y0) / length, -(x1 - x0) / length
        c.stroke([(x0 + (x1 - x0) * t + nx * sag * math.sin(math.pi * t), y0 + (y1 - y0) * t + ny * sag * math.sin(math.pi * t))
                  for t in np.linspace(0, 1, 24)], 4)
    c.save("Icons/Icon_Gum.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 41)
    shoe = [(70, 190), (70, 138), (104, 128), (130, 96), (162, 96), (170, 134), (228, 152), (236, 190)]
    shoe = rotate(shoe, 150, 150, -20, -28, 12)
    c.fill(shoe, opacity=0.35, hatch=(-30, 9))
    c.stroke(shoe, 9, closed=True)
    c.stroke(rotate([(64, 202), (240, 202)], 150, 150, -20, -28, 12), 12)
    for a in range(0, 360, 45):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        r = 22 if a % 90 == 0 else 15
        c.stroke([(226 + ca * 8, 120 + sa * 8), (226 + ca * r, 120 + sa * r)], 6)
    for y, x0, x1 in ((148, 12, 46), (174, 6, 32)):
        c.stroke([(x0, y), (x1, y)], 7, opacity=0.85)
    for x, y, r in ((30, 226, 11), (44, 238, 8), (14, 236, 7)):
        c.stroke(c.circle_pts(x, y, r), 5, closed=True, opacity=0.85)
    c.stroke([(8, 248), (248, 248)], 7, opacity=0.9)
    c.save("Icons/Icon_Tackle.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 42)
    for i, lift in enumerate((6, 0, 3, 12)):
        finger = c.round_rect_pts(58 + 35 * i, 62 + lift, 93 + 35 * i, 130, 16)
        c.fill(finger, opacity=0.4, hatch=(30, 8))
        c.stroke(finger, 8, closed=True)
    thumb = c.round_rect_pts(52, 130, 172, 164, 16)
    c.fill(thumb, opacity=0.4, hatch=(30, 8))
    c.stroke(thumb, 8, closed=True)
    palm = [(172, 130), (198, 130), (198, 176), (178, 202), (86, 202), (64, 184), (60, 164), (172, 164)]
    c.fill(palm, opacity=0.25, hatch=(30, 8))
    c.stroke([(198, 130), (198, 176), (178, 202), (86, 202), (64, 184), (60, 164)], 8)
    c.stroke([(96, 202), (96, 216)], 8)
    c.stroke([(168, 202), (168, 216)], 8)
    c.stroke(c.round_rect_pts(74, 216, 190, 276, 8), 8, closed=True)
    for x in (108, 132, 156):
        c.stroke([(x, 226), (x, 256)], 7)
    for x0, y0, x1, y1 in ((40, 70, 24, 58), (34, 100, 14, 98), (216, 70, 232, 58), (222, 100, 242, 98)):
        c.stroke([(x0, y0), (x1, y1)], 7)
    c.save("Icons/Icon_Hooligan.png", grain=0.3, color=INK)


    c = Canvas(256, 256, 44)
    wall = [(180, 20), (236, 20), (236, 236), (180, 236)]
    c.fill(wall, opacity=0.3, hatch=(45, 10))
    c.stroke(wall, 9, closed=True)
    for row, y in enumerate(range(20, 236, 27)):
        if y > 20:
            c.stroke([(180, y), (236, y)], 5, opacity=0.9)
        for x in ((208,) if row % 2 == 0 else (194, 222)):
            c.stroke([(x, y + 3), (x, min(233, y + 24))], 5, opacity=0.9)
    c.stroke(dense([(58, 194), (172, 110), (70, 52)]), 7, dash=12)
    c.arrow_head((70, 52), math.degrees(math.atan2(52 - 110, 70 - 172)), 26, 8)
    for a in (150, 180, 210):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(172 + ca * 10, 110 + sa * 10), (172 + ca * 26, 110 + sa * 26)], 6)
    c.stroke([(122, 102), (122, 130)], 9)
    c.stroke([(108, 116), (136, 116)], 9)
    ball = c.circle_pts(52, 200, 28)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.save("Icons/Icon_Wall.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 45)
    c.stroke([(128, 248), (128, 150)], 18, rough=0.8)
    c.stroke([(128, 156), (74, 64)], 15, rough=0.8)
    c.stroke([(128, 156), (182, 64)], 15, rough=0.8)
    c.stroke([(74, 64), (56, 196)], 6)
    c.stroke([(182, 64), (56, 196)], 6)
    ball = c.circle_pts(52, 204, 24)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    for x0, y0 in ((196, 44), (214, 64), (222, 92)):
        c.stroke([(x0, y0), (x0 + 22, y0 - 22)], 6, opacity=0.85)
    c.save("Icons/Icon_Slingshot.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 46)
    upper = [(20 + 120 * t, 92 - 34 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 24)]
    lower = [(140 - 120 * t, 92 + 30 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 24)]
    c.stroke(upper + lower, 8, closed=True)
    iris = c.circle_pts(80, 92, 20)
    c.fill(iris, opacity=0.75)
    c.stroke(iris, 6, closed=True)
    for a in (-120, -90, -60):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(80 + ca * 44, 92 + sa * 50), (80 + ca * 58, 92 + sa * 64)], 6)
    curve = [((1 - t) ** 2 * 118 + 2 * (1 - t) * t * 214 + t * t * 204, (1 - t) ** 2 * 124 + 2 * (1 - t) * t * 112 + t * t * 172)
             for t in np.linspace(0, 1, 30)]
    c.stroke(curve, 7, dash=12)
    c.arrow_head(curve[-1], math.degrees(math.atan2(curve[-1][1] - curve[-3][1], curve[-1][0] - curve[-3][0])), 24, 8)
    c.stroke(c.circle_pts(204, 212, 30), 7, closed=True)
    c.fill(c.circle_pts(204, 212, 10), opacity=0.9)
    c.save("Icons/Icon_EagleEye.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 47)
    c.stroke([(12, 224), (244, 224)], 8)
    for x, angle in ((50, 50), (122, 25), (196, 0)):
        tile = rotate(c.round_rect_pts(x - 20, 124, x + 20, 220, 8), x + 20, 220, angle)
        c.fill(tile, opacity=0.35, hatch=(-30, 8))
        c.stroke(tile, 8, closed=True)
        c.stroke(rotate([(x - 12, 172), (x + 12, 172)], x + 20, 220, angle), 5)
        for dy in (148, 196):
            dot = rotate(c.circle_pts(x, dy, 6), x + 20, 220, angle)
            c.fill(dot, opacity=0.9)
    for x0, y0 in ((40, 100), (30, 124)):
        c.stroke([(x0, y0), (x0 - 20, y0 + 6)], 6, opacity=0.8)
    c.save("Icons/Icon_Domino.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 48)
    body = [(124 + 82 * math.cos(a), 158 + 56 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 60, endpoint=False)]
    c.fill(body, opacity=0.35, hatch=(30, 9))
    c.stroke(body, 9, closed=True)
    snout = c.circle_pts(210, 160, 18)
    c.stroke(snout, 8, closed=True)
    for y in (154, 168):
        c.fill(c.circle_pts(210, y, 4), opacity=0.9)
    c.stroke([(92, 110), (108, 78), (126, 104)], 8)
    c.fill(c.circle_pts(172, 136, 6), opacity=0.9)
    for x in (72, 104, 148, 176):
        c.stroke([(x, 206), (x, 230)], 12)
    c.stroke([(44, 150), (30, 142), (24, 154), (34, 162), (28, 172)], 6)
    c.stroke([(106, 104), (148, 104)], 7)
    coin = c.circle_pts(128, 42, 24)
    c.fill(coin, opacity=0.4, hatch=(30, 7))
    c.stroke(coin, 7, closed=True)
    for y in (34, 42):
        c.stroke([(116, y), (140, y)], 5)
    c.stroke([(128, 42), (128, 56)], 5)
    c.save("Icons/Icon_Piggy.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 49)
    lid = c.circle_pts(108, 142, 80)
    c.fill(lid, opacity=0.3, hatch=(30, 9))
    c.stroke(lid, 9, closed=True)
    c.stroke(c.circle_pts(108, 142, 60), 5, closed=True, opacity=0.7)
    knob = c.round_rect_pts(90, 130, 126, 154, 10)
    c.fill(knob, opacity=0.8)
    c.stroke(knob, 6, closed=True)
    c.stroke(dense([(190, 112), (214, 64)]), 7, dash=11)
    for a in (-60, -20, 20):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(190 + ca * 12, 118 + sa * 12), (190 + ca * 28, 118 + sa * 28)], 6)
    ball = c.circle_pts(220, 44, 22)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.save("Icons/Icon_Lid.png", grain=0.3, color=INK)


    c = Canvas(256, 256, 51)
    rope = [(64 + 128 * t, 196 - 170 * math.sin(math.pi * t)) for t in np.linspace(0, 1, 40)]
    c.stroke(rope, 7)
    for x, a in ((64, 20), (192, -20)):
        handle = rotate(c.round_rect_pts(x - 11, 192, x + 11, 244, 9), x, 196, a)
        c.fill(handle, opacity=0.45, hatch=(30, 8))
        c.stroke(handle, 8, closed=True)
    for y, x0 in ((70, 16), (100, 8), (130, 16)):
        c.stroke([(x0, y), (x0 + 30, y)], 6, opacity=0.8)
        c.stroke([(256 - x0, y), (226 - x0, y)], 6, opacity=0.8)
    c.save("Icons/Icon_JumpRope.png", grain=0.3, color=INK)





    c = Canvas(256, 256, 56)
    for cx, cy, a in ((96, 178, -8), (160, 150, 10)):
        pie = rotate([(cx + 62 * math.cos(t), cy + 30 * math.sin(t)) for t in np.linspace(0, 2 * math.pi, 50, endpoint=False)], cx, cy, a)
        c.fill(pie, opacity=0.35, hatch=(30, 9))
        c.stroke(pie, 9, closed=True)
        seam = rotate([(cx - 44 + 88 * t, cy - 14 + 4 * math.sin(t * 14)) for t in np.linspace(0, 1, 30)], cx, cy, a)
        c.stroke(seam, 5)
    for x in (100, 136, 172):
        c.stroke([(x + 9 * math.sin(i * 1.1), 108 - i * 12) for i in range(7)], 7)
    c.save("Icons/Icon_Pies.png", grain=0.3, color=INK)



    c = Canvas(256, 256, 59)
    ball = c.circle_pts(62, 128, 32)
    c.fill(ball, opacity=0.5, hatch=(30, 8))
    c.stroke(ball, 8, closed=True)
    c.stroke(c.circle_pts(40, 128, 30, -60, 60, 12), 5)
    for tip, kids in (((150, 70), ((214, 36), (222, 92))), ((150, 186), ((214, 164), (222, 220)))):
        c.stroke([(96, 128), tip], 6)
        b = c.circle_pts(tip[0], tip[1], 18)
        c.fill(b, opacity=0.5, hatch=(30, 7))
        c.stroke(b, 7, closed=True)
        for k in kids:
            c.stroke([(tip[0] + 18, tip[1]), (k[0] - 12, k[1])], 5, opacity=0.85)
            kb = c.circle_pts(k[0], k[1], 11)
            c.fill(kb, opacity=0.7)
            c.stroke(kb, 5, closed=True)
    c.save("Icons/Icon_Hail.png", grain=0.3, color=INK)

    c = Canvas(256, 256, 60)
    bell = [(128 + 70 * math.cos(a), 168 + 58 * math.sin(a)) for a in np.linspace(math.radians(-60), math.radians(240), 40)]
    c.fill(bell, opacity=0.45, hatch=(30, 9))
    c.stroke(bell, 10, closed=True)
    c.stroke(c.circle_pts(128, 96, 44, 180, 360, 24), 14, rough=0.8)
    c.stroke([(84, 96), (92, 124)], 12)
    c.stroke([(172, 96), (164, 124)], 12)
    c.stroke([(16, 236), (240, 236)], 8)
    for x, y in ((32, 110), (224, 110), (40, 190), (216, 196)):
        for k in range(5):
            a = math.radians(-90 + k * 72)
            c.stroke([(x, y), (x + math.cos(a) * 14, y + math.sin(a) * 14)], 5)
    c.save("Icons/Icon_Kettlebell.png", grain=0.3, color=INK)



    c = Canvas(256, 256, 63)
    wrap = [(40, 90), (84, 106), (172, 106), (216, 90), (206, 128), (216, 166), (172, 150), (84, 150), (40, 166), (50, 128)]
    c.fill(wrap, opacity=0.25, hatch=(30, 10))
    c.stroke(wrap, 9, closed=True)
    c.stroke([(84, 106), (84, 150)], 6)
    c.stroke([(172, 106), (172, 150)], 6)
    for dx, dy in ((0, -14), (14, 0), (0, 14), (-14, 0)):
        leaf = c.circle_pts(128 + dx, 128 + dy, 11)
        c.fill(leaf, opacity=0.8)
    c.stroke([(128, 128), (138, 152)], 5)
    for x, y in ((56, 44), (200, 40), (224, 214), (40, 214)):
        for k in range(4):
            a = math.radians(k * 45)
            c.stroke([(x - math.cos(a) * 11, y - math.sin(a) * 11), (x + math.cos(a) * 11, y + math.sin(a) * 11)], 5)
    c.save("Icons/Icon_LuckyWrapper.png", grain=0.3, color=INK)


def icons_cards_2026():
    """Карточки 2026-10: умения, пассивки, комбо. Умения сохраняются ещё и белыми для HUD (HUD_Ability_*)."""
    ink = 11

    def ball(c, x, y, r, hatch=True):
        pts = c.circle_pts(x, y, r)
        if hatch:
            c.fill(pts, opacity=0.5, hatch=(30, 8))
        c.stroke(pts, 8, closed=True)

    def out(c, name, hud=False):
        c.save(f"Icons/Icon_{name}.png", grain=0.3, color=INK)
        if hud:
            c.save(f"HUD_Ability_{name}.png", grain=0.3, color=(255, 255, 255))

    def digit(c, d, x, y, h, w=10):
        s = h / 2
        shapes = {
            "1": [[(x - s * 0.3, y - s * 0.6), (x, y - s), (x, y + s)]],
            "2": [[(x - s * 0.6, y - s * 0.5)] + [(x + s * 0.55 * math.cos(a), y - s * 0.45 + s * 0.5 * math.sin(a))
                  for a in np.linspace(-2.6, 0.6, 12)] + [(x - s * 0.6, y + s), (x + s * 0.65, y + s)]],
            "3": [[(x + s * 0.55 * math.cos(a), y - s * 0.5 + s * 0.48 * math.sin(a)) for a in np.linspace(-2.8, 1.6, 14)],
                  [(x + s * 0.6 * math.cos(a), y + s * 0.48 + s * 0.52 * math.sin(a)) for a in np.linspace(-1.6, 2.8, 14)]],
        }
        for line in shapes[d]:
            c.stroke(line, w)

    # Забор
    c = Canvas(256, 256, 200)
    for i in range(5):
        x = 46 + i * 38
        plank = [(x - 14, 222), (x + 14, 222), (x + 14, 96), (x, 76), (x - 14, 96)]
        c.fill(plank, opacity=0.35, hatch=(60, 9))
        c.stroke(plank, 8, closed=True)
    for y in (126, 190):
        c.stroke([(22, y), (234, y)], 9)
    ball(c, 214, 46, 20)
    c.stroke([(196, 58), (150, 40)], 6, dash=10)
    c.arrow_head((228, 30), -40, 18, 6)
    out(c, "Fence", hud=True)

    # Мыльные пузыри
    c = Canvas(256, 256, 201)
    for x, y, r in ((104, 140, 70), (196, 70, 34), (206, 182, 24), (52, 54, 18)):
        c.stroke(c.circle_pts(x, y, r), 7, closed=True)
        c.stroke(c.circle_pts(x, y, r * 0.72, 200, 250, 10), 6)
    ball(c, 104, 146, 30)
    out(c, "Bubbles", hud=True)

    # Пугач
    c = Canvas(256, 256, 202)
    gun = [(40, 104), (180, 104), (180, 136), (120, 136), (104, 200), (70, 200), (82, 136), (40, 136)]
    c.fill(gun, opacity=0.4, hatch=(30, 8))
    c.stroke(gun, 9, closed=True)
    c.stroke(c.circle_pts(124, 120, 12), 6, closed=True)
    for a in (-40, -15, 10, 35):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(196 + ca * 14, 120 + sa * 14), (196 + ca * 50, 120 + sa * 50)], 8)
    out(c, "CapGun", hud=True)

    # Фотоаппарат «Смена»
    c = Canvas(256, 256, 203)
    body = c.round_rect_pts(36, 92, 220, 196, 14)
    c.fill(body, opacity=0.35, hatch=(30, 9))
    c.stroke(body, 9, closed=True)
    c.stroke(c.circle_pts(128, 146, 40), 9, closed=True)
    c.stroke(c.circle_pts(128, 146, 18), 6, closed=True)
    c.stroke([(168, 92), (168, 70), (206, 70), (206, 92)], 8)
    for a in (-150, -120, -90, -60, -30):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(187 + ca * 34, 60 + sa * 34), (187 + ca * 54, 60 + sa * 54)], 6)
    out(c, "Camera", hud=True)

    # Магнит
    c = Canvas(256, 256, 204)
    arc = c.circle_pts(100, 120, 56, 90, 270, 30)
    c.stroke(arc, 30, rough=0.6)
    c.stroke([(100, 64), (150, 64)], 30, rough=0.6)
    c.stroke([(100, 176), (150, 176)], 30, rough=0.6)
    for y in (64, 176):
        c.stroke([(136, y - 15), (136, y + 15)], 5, opacity=0.9)
    ball(c, 220, 120, 24)
    for k, r in enumerate((26, 44)):
        c.stroke(c.circle_pts(220, 120, r + 18, 150, 210, 10), 5, opacity=0.8)
    out(c, "Magnet", hud=True)

    # Перемотка
    c = Canvas(256, 256, 205)
    tape = c.round_rect_pts(34, 60, 222, 176, 10)
    c.fill(tape, opacity=0.25, hatch=(40, 10))
    c.stroke(tape, 9, closed=True)
    for x in (92, 164):
        c.stroke(c.circle_pts(x, 116, 20), 7, closed=True)
    c.stroke([(72, 148), (184, 148)], 6)
    for x0 in (150, 104):
        tri = [(x0, 196), (x0 + 44, 214 - 40), (x0 + 44, 236)]
        tri = [(x0, 216), (x0 + 40, 196), (x0 + 40, 236)]
        c.fill(tri, opacity=0.8)
        c.stroke(tri, 6, closed=True)
    out(c, "Rewind", hud=True)

    # Карусель
    c = Canvas(256, 256, 206)
    c.stroke(c.circle_pts(128, 128, 22), 8, closed=True)
    for k in range(6):
        a = math.radians(k * 60)
        ball(c, 128 + math.cos(a) * 82, 128 + math.sin(a) * 82, 16)
    for a0 in (20, 200):
        arc = c.circle_pts(128, 128, 52, a0, a0 + 120, 24)
        c.stroke(arc, 7)
        c.arrow_head(arc[-1], math.degrees(math.atan2(arc[-1][1] - arc[-3][1], arc[-1][0] - arc[-3][0])), 20, 7)
    out(c, "Carousel", hud=True)

    # Резиночка
    c = Canvas(256, 256, 207)
    for x in (34, 222):
        c.stroke(c.circle_pts(x, 96, 16), 7, closed=True)
        c.stroke([(x, 112), (x, 170), (x - 16, 214)], 7)
        c.stroke([(x, 170), (x + 16, 214)], 7)
    band = [(40 + 176 * t, 196 + 10 * math.sin(t * math.pi * 6)) for t in np.linspace(0, 1, 40)]
    c.stroke(band, 7)
    blob = c.circle_pts(128, 150, 30)
    c.fill(blob, opacity=0.4, hatch=(30, 8))
    c.stroke(blob, 7, closed=True)
    for a in (-120, -90, -60):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        c.stroke([(128 + ca * 40, 150 + sa * 40), (128 + ca * 58, 150 + sa * 58)], 6)
    out(c, "ElasticBand", hud=True)

    # Рация
    c = Canvas(256, 256, 208)
    radio = c.round_rect_pts(56, 92, 126, 232, 10)
    c.fill(radio, opacity=0.35, hatch=(30, 9))
    c.stroke(radio, 9, closed=True)
    c.stroke([(108, 92), (108, 30)], 8)
    for y in (122, 138, 154):
        c.stroke([(72, y), (110, y)], 5)
    c.stroke(c.circle_pts(190, 160, 34), 7, closed=True)
    c.stroke(c.circle_pts(190, 160, 12), 6, closed=True)
    for d in ((0, -1), (0, 1), (-1, 0), (1, 0)):
        c.stroke([(190 + d[0] * 40, 160 + d[1] * 40), (190 + d[0] * 54, 160 + d[1] * 54)], 6)
    for r in (22, 40):
        c.stroke(c.circle_pts(108, 30, r, -60, 0, 10), 6)
    out(c, "Walkie", hud=True)


    # Считалочка
    c = Canvas(256, 256, 210)
    for i, d in enumerate("123"):
        digit(c, d, 52 + i * 70, 112 + (i == 2) * -10, 92 + (i == 2) * 20, w=10 + (i == 2) * 2)
    c.stroke([(214, 70), (214, 152)], 10)
    c.stroke(c.circle_pts(214, 172, 5), 8, closed=True)
    ball(c, 186, 220, 18)
    c.stroke([(40, 222), (156, 222)], 6, dash=12)
    out(c, "CountRhyme")



    # Тамагочи
    c = Canvas(256, 256, 213)
    egg = [(128 + 72 * math.cos(a) * (1 - 0.12 * math.sin(a)), 136 + 92 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 60, endpoint=False)]
    c.fill(egg, opacity=0.25, hatch=(30, 10))
    c.stroke(egg, 9, closed=True)
    screen = c.round_rect_pts(90, 92, 166, 152, 6)
    c.stroke(screen, 7, closed=True)
    for x, y in ((112, 112), (144, 112), (116, 134), (128, 138), (140, 134)):
        c.fill(c.round_rect_pts(x - 4, y - 4, x + 4, y + 4, 1), opacity=1.0)
    for x in (100, 128, 156):
        c.stroke(c.circle_pts(x, 186, 9), 6, closed=True)
    c.stroke(c.heart_pts(212, 52, 1.6), 7, closed=True)
    out(c, "Tamagotchi")


    # Круговая порука
    c = Canvas(256, 256, 215)
    for k in range(6):
        a = math.radians(k * 60 - 90)
        x, y = 128 + math.cos(a) * 84, 128 + math.sin(a) * 84
        palm = c.circle_pts(x, y, 16)
        c.fill(palm, opacity=0.5)
        c.stroke(palm, 6, closed=True)
        for f in (-1, 0, 1):
            fa = a + f * 0.45
            c.stroke([(x + math.cos(fa) * 14, y + math.sin(fa) * 14), (x + math.cos(fa) * 30, y + math.sin(fa) * 30)], 5)
    c.stroke(c.circle_pts(128, 112, 16), 7, closed=True)
    c.stroke([(128, 128), (128, 168)], 7)
    c.stroke([(108, 146), (148, 146)], 7)
    out(c, "CircleGuard")



def icons_deck_2026_10_12():
    """Колода 2026-10-12: «Замри!» вобрал свисток, «Второе дыхание» — встаёшь сам через 3 с."""

    def snowflake(c, cx, cy, arm, width=9):
        for k in range(6):
            a = math.radians(90 + k * 60)
            ca, sa = math.cos(a), math.sin(a)
            c.stroke([(cx, cy), (cx + ca * arm, cy - sa * arm)], width)
            px, py = cx + ca * arm * 0.6, cy - sa * arm * 0.6
            for side in (-1, 1):
                b = a + side * math.radians(42)
                c.stroke([(px, py), (px + math.cos(b) * arm * 0.3, py - math.sin(b) * arm * 0.3)], width - 3)

    # «Замри!»: свисток, звук и снежинка
    c = Canvas(256, 256, 310)
    whistle = [(30, 150), (126, 150), (126, 142), (150, 142), (150, 164)]
    whistle += [(124 + 32 * math.cos(a), 180 + 32 * math.sin(a)) for a in np.linspace(math.radians(-20), math.radians(200), 20)]
    whistle += [(30, 182)]
    c.fill(whistle, opacity=0.45, hatch=(30, 8))
    c.stroke(whistle, 9, closed=True)
    c.stroke(c.circle_pts(124, 180, 11), 5, closed=True)
    c.stroke([(36, 150), (52, 100), (96, 76)], 5, opacity=0.85)
    for r in (24, 42):
        c.stroke(c.circle_pts(170, 182, r, -40, 40, 10), 6)
    snowflake(c, 184, 74, 50)
    c.save("Icons/Icon_FreezeWhistle.png", grain=0.3, color=INK)

    # «Второе дыхание»: сердце со стрелкой вверх и секундомер «3»
    c = Canvas(256, 256, 311)
    heart = c.heart_pts(108, 164, 5.0)
    c.fill(heart, opacity=0.35, hatch=(30, 9))
    c.stroke(heart, 10, closed=True)
    c.stroke([(108, 196), (108, 36)], 13)
    c.arrow_head((108, 30), -90, 40, 13)
    cx, cy, r = 204, 72, 34
    c.stroke(c.circle_pts(cx, cy, r), 7, closed=True)
    c.stroke([(cx - 8, cy - r - 10), (cx + 8, cy - r - 10)], 6)
    c.stroke([(cx, cy - r - 10), (cx, cy - r)], 6)
    s = 20 / 2
    c.stroke([(cx + s * 0.55 * math.cos(a), cy - s * 0.5 + s * 0.48 * math.sin(a)) for a in np.linspace(-2.8, 1.6, 14)], 6)
    c.stroke([(cx + s * 0.6 * math.cos(a), cy + s * 0.48 + s * 0.52 * math.sin(a)) for a in np.linspace(-1.6, 2.8, 14)], 6)
    c.save("Icons/Icon_SecondWindUp.png", grain=0.3, color=INK)


def icons_combos_2026_10_12():
    """Комбо 2026-10-12: Подача, Данк, Смэш, Пенальти, Солнышко, Петарда, Чехарда, Штаб, Морская фигура (разбитая)."""

    def ball(c, x, y, r, hatch=True, width=8):
        pts = c.circle_pts(x, y, r)
        if hatch:
            c.fill(pts, opacity=0.5, hatch=(30, 8))
        c.stroke(pts, width, closed=True)

    def arc(x0, y0, x1, y1, height, n=40):
        return [(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t - math.sin(t * math.pi) * height) for t in np.linspace(0, 1, n)]

    def star(c, x, y, r, width=5, rays=6):
        for k in range(rays):
            a = math.radians(k * 360 / rays + 15)
            c.stroke([(x + math.cos(a) * r * 0.35, y + math.sin(a) * r * 0.35), (x + math.cos(a) * r, y + math.sin(a) * r)], width)

    def figure(c, x, y, s=1.0, width=7, arms=(-0.6, 0.6)):
        c.stroke(c.circle_pts(x, y - 70 * s, 15 * s), width, closed=True)
        c.stroke([(x, y - 55 * s), (x, y - 18 * s)], width)
        c.stroke([(x, y - 18 * s), (x - 18 * s, y + 18 * s)], width)
        c.stroke([(x, y - 18 * s), (x + 18 * s, y + 18 * s)], width)
        for a in arms:
            c.stroke([(x, y - 45 * s), (x + math.sin(a) * 34 * s, y - 45 * s - math.cos(a) * 14 * s)], width)

    def out(c, name, hud=False):
        c.save(f"Icons/Icon_{name}.png", grain=0.3, color=INK)
        if hud:
            c.save(f"HUD_Ability_{name}.png", grain=0.3, color=(255, 255, 255))

    # Подача: волейбольный мяч перелетает по четырём врагам
    c = Canvas(256, 256, 320)
    ball(c, 52, 186, 30)
    c.stroke(c.circle_pts(52, 186, 30, 200, 320, 12), 5)
    c.stroke(c.circle_pts(52, 186, 30, 20, 140, 12), 5)
    hops = [(52, 186), (104, 128), (150, 176), (196, 112), (230, 168)]
    for (x0, y0), (x1, y1) in zip(hops[:-1], hops[1:]):
        c.stroke(dense(arc(x0, y0 - 20, x1, y1 - 22, 34)), 6, dash=11)
    for x, y in hops[1:]:
        ball(c, x, y, 12, hatch=False, width=6)
    c.arrow_head((230, 146), 70, 16, 6)
    out(c, "Serve")

    # Данк: мяч падает навесом и подпрыгивает к следующему — два удара по площади
    c = Canvas(256, 256, 321)
    ball(c, 58, 52, 26)
    c.stroke(c.circle_pts(58, 52, 26, 250, 290, 6) + c.circle_pts(58, 52, 26, 70, 110, 6), 5)
    c.stroke([(32, 52), (84, 52)], 5)
    c.stroke(dense(arc(70, 74, 110, 206, -10)), 6, dash=11)
    c.stroke(dense(arc(110, 200, 204, 206, 70)), 6, dash=11)
    c.arrow_head((204, 204), 80, 18, 6)
    for x in (110, 204):
        c.stroke([(x + 34 * math.cos(a), 214 + 9 * math.sin(a)) for a in np.linspace(0, math.pi * 2, 36)], 6)
        star(c, x, 192, 22, 5)
    out(c, "Dunk")

    # Смэш: ракетка и шарик, который всё быстрее скачет между стенками
    c = Canvas(256, 256, 322)
    paddle = c.circle_pts(70, 170, 40)
    c.fill(paddle, opacity=0.45, hatch=(30, 8))
    c.stroke(paddle, 8, closed=True)
    c.stroke([(42, 200), (18, 236)], 12)
    c.stroke([(124, 24), (124, 232)], 7)
    c.stroke([(236, 24), (236, 232)], 7)
    zig = [(100, 150), (130, 120), (230, 92), (130, 66), (230, 42)]
    c.stroke(dense(zig), 6, dash=10)
    ball(c, 206, 34, 12, hatch=False, width=6)
    for x, y in ((136, 112), (222, 92), (138, 64)):
        c.stroke([(x - 8, y - 18), (x + 8, y - 18)], 5)
        c.stroke([(x, y - 26), (x, y - 10)], 5)
    out(c, "Smash")

    # Пенальти: футбольный мяч летит в ворота
    c = Canvas(256, 256, 323)
    goal = [(120, 200), (120, 50), (240, 50), (240, 200)]
    c.stroke(goal, 9)
    for k in range(1, 5):
        c.stroke([(120 + k * 24, 54), (120 + k * 24, 196)], 3, opacity=0.6)
        c.stroke([(124, 54 + k * 29), (236, 54 + k * 29)], 3, opacity=0.6)
    ball(c, 62, 182, 30, hatch=False)
    pent = [(62 + 12 * math.cos(math.radians(-90 + k * 72)), 182 + 12 * math.sin(math.radians(-90 + k * 72))) for k in range(5)]
    c.fill(pent, opacity=0.9)
    for y in (158, 182, 206):
        c.stroke([(14, y + 8), (26, y + 4)], 5)
    c.stroke(dense([(94, 168), (150, 128), (186, 112)]), 6, dash=10)
    c.arrow_head((196, 108), -25, 20, 7)
    out(c, "Penalty")

    # Солнышко: мяч на нитке делает круг вокруг ребёнка
    c = Canvas(256, 256, 324)
    figure(c, 128, 168, 0.95, arms=(-1.2, 1.0))
    orbit = [(128 + 96 * math.cos(a), 128 + 96 * math.sin(a)) for a in np.linspace(math.radians(-60), math.radians(250), 60)]
    c.stroke(orbit, 6, dash=12)
    c.arrow_head((128 + 96 * math.cos(math.radians(250)), 128 + 96 * math.sin(math.radians(250))), -20, 18, 6)
    bx, by = 128 + 96 * math.cos(math.radians(-60)), 128 + 96 * math.sin(math.radians(-60))
    ball(c, bx, by, 18)
    c.stroke([(160, 112), (bx - 10, by + 14)], 4, opacity=0.85)
    out(c, "Sun")

    # Петарда: хлопушка с фитилём и отскоки со взрывами
    c = Canvas(256, 256, 325)
    body = rotate(c.round_rect_pts(40, 40, 120, 76, 10), 80, 58, -30)
    c.fill(body, opacity=0.45, hatch=(60, 8))
    c.stroke(body, 8, closed=True)
    fuse = [(114, 26), (128, 14), (140, 22), (152, 10)]
    c.stroke(fuse, 5)
    star(c, 158, 8, 14, 4)
    for x0, y0, x1, h in ((60, 110, 100, 30), (100, 204, 164, 70), (164, 204, 226, 56)):
        c.stroke(dense(arc(x0, y0, x1, 204, h), 2.0), 6, dash=9)
    for x in (100, 164, 226):
        star(c, x, 210, 17, 5, rays=8)
    out(c, "Petard")

    # Чехарда: один ребёнок прыгает через согнувшегося врага
    c = Canvas(256, 256, 326)
    hump = c.circle_pts(128, 206, 42, 180, 360, 24)
    c.fill(hump + [(170, 206), (86, 206)], opacity=0.4, hatch=(30, 9))
    c.stroke(hump, 8)
    c.stroke(c.circle_pts(76, 196, 14), 7, closed=True)
    c.stroke([(24, 232), (232, 232)], 6)
    c.stroke(dense(arc(40, 200, 216, 200, 150)), 6, dash=11)
    c.arrow_head((216, 198), 70, 18, 6)
    c.stroke(c.circle_pts(128, 36, 14), 7, closed=True)
    c.stroke([(128, 50), (128, 80)], 7)
    c.stroke([(128, 62), (96, 48)], 7)
    c.stroke([(128, 62), (160, 48)], 7)
    c.stroke([(128, 80), (100, 96)], 7)
    c.stroke([(128, 80), (156, 96)], 7)
    out(c, "Leapfrog")

    # Штаб: забор буквой П с флажком, мяч отлетает обратно
    c = Canvas(256, 256, 327)
    for i in range(4):
        x = 84 + i * 30
        plank = [(x - 11, 220), (x + 11, 220), (x + 11, 120), (x, 104), (x - 11, 120)]
        c.fill(plank, opacity=0.3, hatch=(60, 9))
        c.stroke(plank, 7, closed=True)
    for side in (-1, 1):
        x0 = 128 + side * 62
        for k in range(2):
            x = x0 + side * (14 + k * 20)
            plank = [(x - 8 * side, 200 - k * 12), (x + 8 * side, 194 - k * 12), (x + 8 * side, 104 - k * 12), (x - 8 * side, 110 - k * 12)]
            c.fill(plank, opacity=0.3, hatch=(60, 9))
            c.stroke(plank, 6, closed=True)
    c.stroke([(128, 104), (128, 30)], 6)
    flag = [(128, 30), (170, 42), (128, 56)]
    c.fill(flag, opacity=0.5, hatch=(30, 7))
    c.stroke(flag, 6, closed=True)
    ball(c, 222, 150, 16)
    c.stroke(dense([(204, 142), (214, 92), (232, 54)]), 6, dash=10)
    c.arrow_head((236, 46), -70, 18, 6)
    out(c, "Fort", hud=True)

    # Морская фигура: замершая фигура трескается, осколки разлетаются
    c = Canvas(256, 256, 328)
    figure(c, 108, 176, 1.0, arms=(-1.3, 1.3))
    c.stroke([(96, 70), (116, 100), (100, 128), (122, 160)], 4)
    shards = [((176, 92), 0.0), ((200, 140), 0.8), ((176, 190), 1.6), ((214, 64), 2.4)]
    for (x, y), a in shards:
        tri = [(x + 14 * math.cos(a), y + 14 * math.sin(a)), (x + 12 * math.cos(a + 2.2), y + 12 * math.sin(a + 2.2)),
               (x + 10 * math.cos(a + 4.1), y + 10 * math.sin(a + 4.1))]
        c.fill(tri, opacity=0.6)
        c.stroke(tri, 5, closed=True)
    for k in range(6):
        a = math.radians(k * 60)
        c.stroke([(214, 196), (214 + math.cos(a) * 26, 196 + math.sin(a) * 26)], 5)
    for x in (150, 162):
        c.stroke([(x, 112), (x + 16, 104)], 4)
    out(c, "Shatter")


def pockets():
    c = Canvas(128, 128, 70)
    frame = wobble(c.round_rect_pts(10, 10, 118, 118, 18), 0.8, 70)
    c.stroke(dense(frame + [frame[0]], 2.0), 5, opacity=0.9, dash=12)
    c.save("Pocket_Slot.png")


def cursor():
    """Курсор мышью: мелом нарисованная стрелка (остриё — (20, 14)) и тёмная подложка под неё."""
    tip = [(20, 14), (21, 98), (43, 77), (58, 114), (77, 105), (62, 70), (93, 70)]
    shape = wobble(tip, 0.9, 80)
    c = Canvas(128, 128, 80)
    c.fill(shape, opacity=0.8, hatch=(35, 6))
    c.stroke(shape, 8, closed=True)
    c.save("Cursor_Chalk.png", grain=0.3, pits=0.04)

    c = Canvas(128, 128, 81)
    c.fill(shape, opacity=1.0, soft=3.0)
    c.stroke(shape, 18, closed=True, rough=0.4)
    c.alpha = blur(c.alpha, 1.6)
    c.save("Cursor_Shadow.png", chalky=False, color=(14, 18, 26))


if __name__ == "__main__":
    hud()
    card_frames()
    icons()
    icons_cards_2026()
    icons_deck_2026_10_12()
    icons_combos_2026_10_12()
    pockets()
    cursor()
