from flask import Flask, jsonify, request
import pymysql
from pymysql.cursors import DictCursor
from werkzeug.security import check_password_hash, generate_password_hash


app = Flask(__name__)
app.config["SECRET_KEY"] = "secret!"


def get_connection():
    return pymysql.connect(
        host="127.0.0.1",
        user="root",
        password="1234",
        db="ar_fit",
        charset="utf8mb4",
        cursorclass=DictCursor,
        autocommit=False,
    )


def ok(data=None, **extra):
    payload = {"ok": True}
    if data is not None:
        payload["data"] = data
    payload.update(extra)
    return jsonify(payload)


def fail(message, status=400):
    return jsonify({"ok": False, "message": message}), status


def require_json():
    return request.get_json(silent=True) or {}


@app.get("/api/health")
def health():
    try:
        with get_connection() as conn:
            with conn.cursor() as cursor:
                cursor.execute("SELECT 1 AS db_ok")
                row = cursor.fetchone()
        return ok({"api": "ok", "db": row["db_ok"] == 1})
    except Exception as exc:
        return fail(str(exc), 500)


@app.get("/api/categories")
def categories():
    with get_connection() as conn:
        with conn.cursor() as cursor:
            cursor.execute(
                """
                SELECT
                    c.category_id,
                    c.category_name,
                    c.category_code,
                    c.parent_category_id,
                    p.category_name AS parent_category_name,
                    c.sort_order
                FROM categories c
                LEFT JOIN categories p ON p.category_id = c.parent_category_id
                ORDER BY c.sort_order ASC, c.category_id ASC
                """
            )
            rows = cursor.fetchall()
    return ok(rows)


@app.get("/api/outfits")
def outfits():
    category_code = request.args.get("category_code")
    gender = request.args.get("gender")
    color = request.args.get("color")
    sort = request.args.get("sort", "unity_index")

    order_by = {
        "unity_index": "o.unity_outfit_index ASC",
        "name": "o.outfit_name ASC",
        "newest": "o.created_at DESC",
        "oldest": "o.created_at ASC",
        "color": "o.color ASC, o.outfit_name ASC",
    }.get(sort, "o.unity_outfit_index ASC")

    where = ["o.is_active = 1"]
    params = []

    if category_code:
        where.append("c.category_code = %s")
        params.append(category_code)

    if gender:
        where.append("(o.gender = %s OR o.gender = 'unisex')")
        params.append(gender)

    if color:
        where.append("o.color = %s")
        params.append(color)

    sql = f"""
        SELECT
            o.outfit_id,
            o.outfit_name,
            o.category_id,
            c.category_name,
            c.category_code,
            o.gender,
            o.color,
            o.description,
            o.unity_category_code,
            o.unity_outfit_index,
            o.unity_outfit_key,
            o.thumbnail_url,
            o.created_at,
            o.updated_at
        FROM outfits o
        JOIN categories c ON c.category_id = o.category_id
        WHERE {" AND ".join(where)}
        ORDER BY {order_by}
    """

    with get_connection() as conn:
        with conn.cursor() as cursor:
            cursor.execute(sql, params)
            rows = cursor.fetchall()

    return ok(rows)


@app.get("/api/outfits/<int:outfit_id>")
def outfit_detail(outfit_id):
    with get_connection() as conn:
        with conn.cursor() as cursor:
            cursor.execute(
                """
                SELECT
                    o.outfit_id,
                    o.outfit_name,
                    o.category_id,
                    c.category_name,
                    c.category_code,
                    o.gender,
                    o.color,
                    o.description,
                    o.unity_category_code,
                    o.unity_outfit_index,
                    o.unity_outfit_key,
                    o.thumbnail_url
                FROM outfits o
                JOIN categories c ON c.category_id = o.category_id
                WHERE o.outfit_id = %s AND o.is_active = 1
                """,
                (outfit_id,),
            )
            row = cursor.fetchone()

    if row is None:
        return fail("outfit not found", 404)

    return ok(row)


@app.post("/api/auth/register")
def register():
    body = require_json()
    name = (body.get("name") or "").strip()
    phone = (body.get("phone") or "").strip()
    password = body.get("password") or ""

    if not name or not phone or not password:
        return fail("name, phone, password are required")

    password_hash = generate_password_hash(password)

    try:
        with get_connection() as conn:
            with conn.cursor() as cursor:
                cursor.execute(
                    """
                    INSERT INTO users (name, phone, password_hash)
                    VALUES (%s, %s, %s)
                    """,
                    (name, phone, password_hash),
                )
                user_id = cursor.lastrowid
            conn.commit()
    except pymysql.err.IntegrityError:
        return fail("phone already exists", 409)

    return ok({"user_id": user_id, "name": name, "phone": phone})


@app.post("/api/auth/login")
def login():
    body = require_json()
    phone = (body.get("phone") or "").strip()
    password = body.get("password") or ""

    if not phone or not password:
        return fail("phone and password are required")

    with get_connection() as conn:
        with conn.cursor() as cursor:
            cursor.execute(
                """
                SELECT user_id, name, phone, password_hash
                FROM users
                WHERE phone = %s
                """,
                (phone,),
            )
            user = cursor.fetchone()

    if user is None or not check_password_hash(user["password_hash"], password):
        return fail("invalid phone or password", 401)

    return ok({"user_id": user["user_id"], "name": user["name"], "phone": user["phone"]})


@app.get("/api/users/<int:user_id>/favorites")
def user_favorites(user_id):
    with get_connection() as conn:
        with conn.cursor() as cursor:
            cursor.execute(
                """
                SELECT
                    f.favorite_id,
                    f.created_at AS favorite_created_at,
                    o.outfit_id,
                    o.outfit_name,
                    o.category_id,
                    c.category_name,
                    c.category_code,
                    o.gender,
                    o.color,
                    o.description,
                    o.unity_category_code,
                    o.unity_outfit_index,
                    o.unity_outfit_key,
                    o.thumbnail_url
                FROM favorites f
                JOIN outfits o ON o.outfit_id = f.outfit_id
                JOIN categories c ON c.category_id = o.category_id
                WHERE f.user_id = %s
                ORDER BY f.created_at DESC
                """,
                (user_id,),
            )
            rows = cursor.fetchall()
    return ok(rows)


@app.post("/api/favorites/toggle")
def toggle_favorite():
    body = require_json()
    user_id = body.get("user_id")
    outfit_id = body.get("outfit_id")

    if not user_id or not outfit_id:
        return fail("user_id and outfit_id are required")

    with get_connection() as conn:
        with conn.cursor() as cursor:
            cursor.execute(
                "SELECT favorite_id FROM favorites WHERE user_id = %s AND outfit_id = %s",
                (user_id, outfit_id),
            )
            existing = cursor.fetchone()

            if existing:
                cursor.execute(
                    "DELETE FROM favorites WHERE favorite_id = %s",
                    (existing["favorite_id"],),
                )
                is_favorite = False
            else:
                cursor.execute(
                    "INSERT INTO favorites (user_id, outfit_id) VALUES (%s, %s)",
                    (user_id, outfit_id),
                )
                is_favorite = True

        conn.commit()

    return ok({"user_id": user_id, "outfit_id": outfit_id, "is_favorite": is_favorite})


@app.post("/api/dev/seed")
def seed_dev_data():
    with get_connection() as conn:
        with conn.cursor() as cursor:
            cursor.execute(
                """
                INSERT INTO categories (category_name, category_code, parent_category_id, sort_order)
                VALUES
                    ('상의', 'upper', NULL, 10),
                    ('하의', 'lower', NULL, 20),
                    ('모자', 'hat', NULL, 30),
                    ('신발', 'shoes', NULL, 40)
                ON DUPLICATE KEY UPDATE
                    category_name = VALUES(category_name),
                    sort_order = VALUES(sort_order)
                """
            )

            cursor.execute(
                """
                INSERT INTO outfits (
                    outfit_name,
                    category_id,
                    gender,
                    color,
                    description,
                    unity_category_code,
                    unity_outfit_index,
                    unity_outfit_key,
                    thumbnail_url
                )
                SELECT '블랙 셔츠', category_id, 'unisex', 'black',
                       '깔끔한 블랙 컬러의 기본 상의입니다.',
                       'upper', 0, 'upper_0', NULL
                FROM categories WHERE category_code = 'upper'
                AND NOT EXISTS (
                    SELECT 1 FROM outfits
                    WHERE unity_category_code = 'upper' AND unity_outfit_index = 0
                )
                """
            )

            cursor.execute(
                """
                INSERT INTO outfits (
                    outfit_name,
                    category_id,
                    gender,
                    color,
                    description,
                    unity_category_code,
                    unity_outfit_index,
                    unity_outfit_key,
                    thumbnail_url
                )
                SELECT '데님 팬츠', category_id, 'unisex', 'blue',
                       'AR 피팅용 기본 데님 하의입니다.',
                       'lower', 0, 'lower_0', NULL
                FROM categories WHERE category_code = 'lower'
                AND NOT EXISTS (
                    SELECT 1 FROM outfits
                    WHERE unity_category_code = 'lower' AND unity_outfit_index = 0
                )
                """
            )

        conn.commit()

    return ok({"seeded": True})


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5000, debug=True)
