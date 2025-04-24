using System.Numerics;

using Raylib_CSharp;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Camera.Cam2D;
using Raylib_CSharp.Collision;
using Raylib_CSharp.Images;
using Raylib_CSharp.Interact;
using Raylib_CSharp.Textures;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Windowing;
using Raylib_CSharp.Transformations;

namespace HelloWorld;

public static class Constants {
    public const int SCREEN_WIDTH = 1280;
    public const int SCREEN_HEIGTH = 720;
}

enum EntityType {
    Player,
    Asteroid,
    SimpleBullet,
}

interface Entity {
    void Update(GameState game, float dt, Vector2 ddP);
}

class SpatialEntity : Entity {
    public EntityType type;
    public Vector2 P, dP;
    public float damping;
    public float width, height;
    public Vector2 forward;
    public Texture2D tex;
    public Rectangle srcRect;
    public bool canCollide;
    public float age, ttl;
    public bool active;

    public void Update(GameState game, float dt, Vector2 ddP){
        age += dt;
        if(ttl > 0 && age > ttl) {
            active = false;
            return;
        }

        float ddPLenth = RayMath.Vector2LengthSqr(ddP);
        if(ddPLenth > 1.0f) {
            ddPLenth *= (float)(1.0/Math.Sqrt(ddPLenth));
        }

        float entitySpeed = 2000.0f;
        ddP = RayMath.Vector2Scale(ddP, entitySpeed);
        ddP = RayMath.Vector2Add(ddP, RayMath.Vector2Scale(dP, damping));

        Vector2 entityDelta = RayMath.Vector2Add(RayMath.Vector2Scale(ddP, 0.5f*dt*dt), RayMath.Vector2Scale(dP, dt));
        dP = RayMath.Vector2Add(RayMath.Vector2Scale(ddP, dt), dP);

        float tMin = 1.0f;
        P = RayMath.Vector2Add(P, RayMath.Vector2Scale(entityDelta, tMin));

        foreach(Entity target in game.entities) {
            SpatialEntity? targetEnt = target as SpatialEntity;
            if(targetEnt == null || !targetEnt.canCollide || this == targetEnt) {
                continue;
            }
            if(ShapeHelper.CheckCollisionCircles(P, width/2, targetEnt.P, targetEnt.width/2)) {
                float cDist = RayMath.Vector2Distance(P, targetEnt.P);
                float overlap = 0.5f * (cDist -width/2 - targetEnt.width/2);
                P.X -= overlap * (P.X - targetEnt.P.X)/cDist;
                P.Y -= overlap * (P.Y - targetEnt.P.Y)/cDist;
                targetEnt.P.X += overlap * (P.X - targetEnt.P.X)/cDist;
                targetEnt.P.Y += overlap * (P.Y - targetEnt.P.Y)/cDist;
            }
        }
    }
}

class GameState {
    public List<Entity> entities;
    public SpatialEntity playerEntity;

    public GameState() {
        // Add Player
        Image ship = Image.Load("res/player_ship.png");
        playerEntity = new SpatialEntity();
        playerEntity.type = EntityType.Player;
        playerEntity.tex = Texture2D.LoadFromImage(ship);
        playerEntity.P = Vector2.Zero;
        playerEntity.dP = Vector2.Zero;
        playerEntity.damping = -2.0f;
        playerEntity.canCollide = true;
        playerEntity.forward = new Vector2(0,-1);
        playerEntity.srcRect = new Rectangle(0,0, (float)ship.Width, (float)ship.Height);
        playerEntity.width = (float)ship.Width;
        playerEntity.height = (float)ship.Height;
        playerEntity.age = 0;
        playerEntity.ttl = -1;
        playerEntity.active = true;
        ship.Unload();

        entities = new List<Entity>();
        entities.Add(playerEntity);

        // Add some asteroids
        for(int i = 0; i < 20; i++) {
            bool safeToAdd = false;
            Vector2 newCenter = Vector2.Zero;
            float newRad = 0f;
            int attempts = 0;
            while(!safeToAdd && attempts < 30) {
                newCenter = new Vector2(Random.Shared.Next(Constants.SCREEN_WIDTH)-Constants.SCREEN_WIDTH/2, Random.Shared.Next(Constants.SCREEN_HEIGTH)-Constants.SCREEN_HEIGTH/2);
                newRad = RayMath.Remap(Random.Shared.NextSingle(), 0, 1, 20, 100);
                safeToAdd = true;
                foreach(Entity rawEnt in entities) {
                    SpatialEntity ent = (SpatialEntity)rawEnt;
                    if(ShapeHelper.CheckCollisionCircles(ent.P, ent.width/2, newCenter, newRad)) {
                        safeToAdd = false;
                        attempts++;
                        break;
                    }
                }
            }
            SpatialEntity ast = new SpatialEntity();
            ast.type = EntityType.Asteroid;
            ast.P = newCenter;
            ast.width = newRad * 2;
            ast.height = newRad * 2;
            ast.canCollide = true;
            ast.active = true;
            ast.dP = new Vector2(Random.Shared.NextSingle()*50-25, Random.Shared.NextSingle()*50-25);
            entities.Add(ast);
        }
    }

};

class Game {
    public static int Main() {
        Window.Init(Constants.SCREEN_WIDTH, Constants.SCREEN_HEIGTH, "Hello World");
        Time.SetTargetFPS(60);

        GameState game = new GameState();
        Vector2 halfScreen = new Vector2(Constants.SCREEN_WIDTH/2, Constants.SCREEN_HEIGTH/2);
        Camera2D cam = new Camera2D(halfScreen, Vector2.Zero, 0, 1);
        float rotSpeed = 9.0f;

        while (!Window.ShouldClose()) {
            float dt = Time.GetFrameTime();
            float rotInput = 0.0f;
            float ddPInput = 0.0f;

            // Handle Input
            if(Input.IsKeyDown(KeyboardKey.W)) {
                ddPInput = 1.0f;
            }
            if(Input.IsKeyDown(KeyboardKey.S)) {
                ddPInput = -1.0f;
            }
            if(Input.IsKeyDown(KeyboardKey.D)) {
                rotInput = rotSpeed * dt;
            }
            if(Input.IsKeyDown(KeyboardKey.A)) {
                rotInput = -rotSpeed * dt;
            }

            // Update
            foreach(Entity rawEnt in game.entities) {
                SpatialEntity ent = (SpatialEntity)rawEnt;

                switch(ent.type) {
                    case EntityType.Asteroid:
                        ent.Update(game, dt, Vector2.Zero);
                        break;
                    case EntityType.Player:
                        Vector2 ddP = RayMath.Vector2Scale(ent.forward, ddPInput);
                        ent.forward = RayMath.Vector2Rotate(ent.forward, rotInput);
                        ent.Update(game, dt, ddP);
                        break;
                }
            }


            // Draw
            Graphics.BeginDrawing();
            Graphics.BeginMode2D(cam);
            Graphics.ClearBackground(Color.Black);

            foreach(Entity rawEnt in game.entities) {
                SpatialEntity ent = (SpatialEntity)rawEnt;

                switch(ent.type) {
                    case EntityType.Player:
                    float rotation = RayMath.Vector2Angle(ent.forward, new Vector2(0,-1));
                    rotation *= -180/(float)Math.PI;
                    float half = ent.srcRect.Height/2.0f;
                    Graphics.DrawTexturePro(ent.tex, ent.srcRect, new Rectangle(ent.P.X, ent.P.Y, half*2, half*2), new Vector2(half, half), rotation, Color.White);
                    break;

                    case EntityType.Asteroid:
                    Graphics.DrawPoly(ent.P, 11, ent.width/2, 0, Color.DarkGray);
                    break;
                }
            }

            Graphics.EndMode2D();
            Graphics.DrawFPS(10, 10);
            Graphics.EndDrawing();
        }

        Window.Close();

        return 0;
    }
}
