extends Label


func _ready():
	pass # Replace with function body.


func _process(delta):
	pass
	self.text = str(Engine.get_frames_per_second()) + " FPS"
